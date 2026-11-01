using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicBooking.Infrastructure.Configuration;

/// <summary>
/// Filtered unique index: only one Booked row per TimeSlotId.
/// Cancelled rows keep history and do not block the slot.
/// </summary>
public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    /// <summary>Filtered unique index is the database lock for PDF "no double bookings".</summary>
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ConfirmationCode).HasMaxLength(16).IsRequired();
        builder.Property(e => e.CancellationReason).HasMaxLength(256);

        builder.HasOne(e => e.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Clinic)
            .WithMany(c => c.Appointments)
            .HasForeignKey(e => e.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TimeSlot)
            .WithOne(s => s.Appointment)
            .HasForeignKey<Appointment>(e => e.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PreviousAppointment)
            .WithMany()
            .HasForeignKey(e => e.PreviousAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TimeSlotId)
            .IsUnique()
            .HasFilter($"[{nameof(Appointment.Status)}] = {(int)AppointmentStatus.Booked}")
            .HasDatabaseName("UX_Appointments_TimeSlotId_Booked");

        builder.HasIndex(e => e.ConfirmationCode).IsUnique();
        builder.HasIndex(e => new { e.PatientId, e.BookedAtUtc });
    }
}
