using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Appointments;

/// <summary>One row on My appointments — includes cancelled/rescheduled history.</summary>
public sealed record AppointmentDto(
    Guid Id,
    Guid ClinicId,
    string ClinicName,
    Guid TimeSlotId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    AppointmentStatus Status,
    string ConfirmationCode);

/// <summary>List bookings for the JWT user. Query = read, no writes.</summary>
public sealed record GetMyAppointmentsQuery(string UserId) : IRequest<IReadOnlyList<AppointmentDto>>;

/// <summary>Newest slot first. Staff have no Patient row → 404 (they should use the diary). sealed: leaf handler.</summary>
public sealed class GetMyAppointmentsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetMyAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    /// <summary>Projects in SQL so we do not load full entity graphs into memory.</summary>
    public async Task<IReadOnlyList<AppointmentDto>> Handle(
        GetMyAppointmentsQuery request,
        CancellationToken cancellationToken)
    {
        var patient = await db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Patient profile not found.");

        return await db.Appointments
            .AsNoTracking()
            .Where(a => a.PatientId == patient.Id)
            .OrderByDescending(a => a.TimeSlot.StartUtc)
            .Select(a => new AppointmentDto(
                a.Id,
                a.ClinicId,
                a.Clinic.Name,
                a.TimeSlotId,
                a.TimeSlot.StartUtc,
                a.TimeSlot.EndUtc,
                a.Status,
                a.ConfirmationCode))
            .ToListAsync(cancellationToken);
    }
}
