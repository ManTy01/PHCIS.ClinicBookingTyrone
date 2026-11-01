using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Appointments;

/// <summary>What the UI shows after a successful book (PDF: confirmation messages).</summary>
public sealed record BookAppointmentResult(Guid AppointmentId, string ConfirmationCode, DateTimeOffset StartUtc);

/// <summary>
/// Book this Identity user onto this slot. UserId comes from the JWT, not the JSON body
/// (so you cannot book as someone else).
/// sealed: MediatR exact type.
/// </summary>
public sealed record BookAppointmentCommand(string UserId, Guid TimeSlotId) : IRequest<BookAppointmentResult>;

/// <summary>PDF FluentValidation — both ids required before we touch SQL.</summary>
public sealed class BookAppointmentCommandValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TimeSlotId).NotEmpty();
    }
}

/// <summary>
/// PDF: booking logic + no double bookings.
/// 1) Slot must be Available and in the future.
/// 2) Flip to Booked and insert Appointment in one SaveChanges.
/// 3) Unique index UX_Appointments_TimeSlotId_Booked is the race backup → 409.
/// 4) SignalR tells other browsers the cell is Booked.
/// sealed: leaf handler.
/// </summary>
public sealed class BookAppointmentCommandHandler(IAppDbContext db, ISlotBoardNotifier board)
    : IRequestHandler<BookAppointmentCommand, BookAppointmentResult>
{
    /// <summary>Success returns PHC-…… confirmation. Taken slot throws ConflictException.</summary>
    public async Task<BookAppointmentResult> Handle(
        BookAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var patient = await db.Patients
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Patient profile not found.");

        var slot = await db.TimeSlots
            .FirstOrDefaultAsync(s => s.Id == request.TimeSlotId, cancellationToken)
            ?? throw new NotFoundException("Time slot not found.");

        if (slot.Status != TimeSlotStatus.Available || slot.StartUtc <= DateTimeOffset.UtcNow)
        {
            throw new ConflictException("That time slot is not available.");
        }

        slot.Status = TimeSlotStatus.Booked;

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ClinicId = slot.ClinicId,
            TimeSlotId = slot.Id,
            Status = AppointmentStatus.Booked,
            ConfirmationCode = ConfirmationCodes.NewCode(),
            BookedAtUtc = DateTimeOffset.UtcNow
        };

        db.Appointments.Add(appointment);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("That time slot was just booked.");
        }

        await board.NotifySlotChangedAsync(slot.ClinicId, slot.Id, TimeSlotStatus.Booked, cancellationToken);
        return new BookAppointmentResult(appointment.Id, appointment.ConfirmationCode, slot.StartUtc);
    }

    /// <summary>SQL unique-index failure (two patients, one slot). Message text varies by provider.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UX_Appointments_TimeSlotId_Booked", StringComparison.OrdinalIgnoreCase);
    }
}
