using ClinicBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Abstractions;

/// <summary>
/// What Application is allowed to see of EF. The SQL Server provider stays in Infrastructure.
/// Handlers depend on this interface so tests can Moq it (PDF: Moq).
/// </summary>
public interface IAppDbContext
{
    DbSet<Clinic> Clinics { get; }
    DbSet<Patient> Patients { get; }
    DbSet<TimeSlot> TimeSlots { get; }
    DbSet<Appointment> Appointments { get; }

    /// <summary>Commit the current tracking changes (book/cancel/reschedule).</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
