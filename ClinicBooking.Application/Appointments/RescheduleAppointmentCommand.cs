using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Appointments;

/// <summary>
/// Keeps the old row (Status = Rescheduled), frees that slot, and inserts a new Booked row.
/// Same pattern as OZ corrections: we do not UPDATE the original booking in place.
/// </summary>
public sealed record RescheduleAppointmentCommand(string UserId, Guid AppointmentId, Guid NewTimeSlotId)
    : IRequest<BookAppointmentResult>;

/// <summary>PDF FluentValidation — three ids required.</summary>
public sealed class RescheduleAppointmentCommandValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.NewTimeSlotId).NotEmpty();
    }
}

/// <summary>
/// PDF: rescheduling. Old appointment stays in history with PreviousAppointmentId on the new one.
/// New slot uses the same unique-index 409 path as Book.
/// sealed: leaf handler.
/// </summary>
public sealed class RescheduleAppointmentCommandHandler(IAppDbContext db, ISlotBoardNotifier board)
    : IRequestHandler<RescheduleAppointmentCommand, BookAppointmentResult>
{
    /// <summary>Returns the new confirmation code. Same-slot or taken slot → 409.</summary>
    public async Task<BookAppointmentResult> Handle(
        RescheduleAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var patient = await db.Patients
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Patient profile not found.");

        var existing = await db.Appointments
            .Include(a => a.TimeSlot)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken)
            ?? throw new NotFoundException("Appointment not found.");

        if (existing.PatientId != patient.Id)
        {
            throw new NotFoundException("Appointment not found.");
        }

        if (existing.Status != AppointmentStatus.Booked)
        {
            throw new ConflictException("Only a booked appointment can be rescheduled.");
        }

        if (existing.TimeSlotId == request.NewTimeSlotId)
        {
            throw new ConflictException("Pick a different time slot.");
        }

        var newSlot = await db.TimeSlots
            .FirstOrDefaultAsync(s => s.Id == request.NewTimeSlotId, cancellationToken)
            ?? throw new NotFoundException("Time slot not found.");

        if (newSlot.Status != TimeSlotStatus.Available || newSlot.StartUtc <= DateTimeOffset.UtcNow)
        {
            throw new ConflictException("That time slot is not available.");
        }

        existing.Status = AppointmentStatus.Rescheduled;
        existing.TimeSlot.Status = TimeSlotStatus.Available;

        newSlot.Status = TimeSlotStatus.Booked;

        var next = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ClinicId = newSlot.ClinicId,
            TimeSlotId = newSlot.Id,
            Status = AppointmentStatus.Booked,
            ConfirmationCode = ConfirmationCodes.NewCode(),
            BookedAtUtc = DateTimeOffset.UtcNow,
            PreviousAppointmentId = existing.Id
        };

        db.Appointments.Add(next);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("That time slot was just booked.");
        }

        await board.NotifySlotChangedAsync(
            existing.ClinicId,
            existing.TimeSlotId,
            TimeSlotStatus.Available,
            cancellationToken);
        await board.NotifySlotChangedAsync(
            newSlot.ClinicId,
            newSlot.Id,
            TimeSlotStatus.Booked,
            cancellationToken);
        return new BookAppointmentResult(next.Id, next.ConfirmationCode, newSlot.StartUtc);
    }

    /// <summary>Same unique-index detection as BookAppointmentCommandHandler.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UX_Appointments_TimeSlotId_Booked", StringComparison.OrdinalIgnoreCase);
    }
}
