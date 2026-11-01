using ClinicBooking.Application.Abstractions;
using ClinicBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Infrastructure.Persistence;

/// <summary>
/// EF door to SQL Server / LocalDB (PDF: EF Core). Identity tables come from the base class
/// (AspNetUsers, AspNetRoles, …). Clinic/Patient/slot/appointment maps live in Configuration/.
/// Not sealed: IdentityDbContext is designed to be subclassed; this IS that subclass.
/// </summary>
public class AppDbContext : IdentityDbContext<IdentityUser>, IAppDbContext
{
    /// <summary>Options include the connection string from Program.cs / Docker env.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    /// <summary>Apply IEntityTypeConfiguration classes in this assembly (indexes, seed clinics).</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
