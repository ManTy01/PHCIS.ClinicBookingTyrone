using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Appointments;

/// <summary>Cancel own booking. UserId from JWT so you cannot cancel someone else's row.</summary>
public sealed record CancelAppointmentCommand(string UserId, Guid AppointmentId, string? Reason)
    : IRequest<Unit>;

/// <summary>PDF FluentValidation — ids required; reason is optional.</summary>
public sealed class CancelAppointmentCommandValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
    }
}

/// <summary>
/// Marks the appointment Cancelled (history kept) and sets the slot back to Available.
/// We do not DELETE — keeping event rows.
/// sealed: leaf handler.
/// </summary>
public sealed class CancelAppointmentCommandHandler(IAppDbContext db, ISlotBoardNotifier board)
    : IRequestHandler<CancelAppointmentCommand, Unit>
{
    /// <summary>Wrong owner looks like 404 (do not leak that the id exists). Already cancelled → 409.</summary>
    public async Task<Unit> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var patient = await db.Patients
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Patient profile not found.");

        var appointment = await db.Appointments
            .Include(a => a.TimeSlot)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken)
            ?? throw new NotFoundException("Appointment not found.");

        if (appointment.PatientId != patient.Id)
        {
            throw new NotFoundException("Appointment not found.");
        }

        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new ConflictException("Only a booked appointment can be cancelled.");
        }

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancellationReason = request.Reason;
        appointment.TimeSlot.Status = TimeSlotStatus.Available;

        await db.SaveChangesAsync(cancellationToken);
        await board.NotifySlotChangedAsync(
            appointment.ClinicId,
            appointment.TimeSlotId,
            TimeSlotStatus.Available,
            cancellationToken);
        return Unit.Value;
    }
}
