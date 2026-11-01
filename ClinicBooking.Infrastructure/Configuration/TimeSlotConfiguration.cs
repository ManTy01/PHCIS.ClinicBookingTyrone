using ClinicBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicBooking.Infrastructure.Configuration;

/// <summary>
/// Unique (ClinicId, StartUtc) so a clinic cannot have two rows for 09:00.
/// That is one half of "no double booking"; the other half is the appointment index.
/// </summary>
public sealed class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    /// <summary>Status stored as int. Unique (ClinicId, StartUtc) stops two 09:00 rows at one clinic.</summary>
    public void Configure(EntityTypeBuilder<TimeSlot> builder)
    {
        builder.ToTable("TimeSlots");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).HasConversion<int>();

        builder.HasOne(e => e.Clinic)
            .WithMany(c => c.TimeSlots)
            .HasForeignKey(e => e.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ClinicId, e.StartUtc }).IsUnique();
        builder.HasIndex(e => new { e.ClinicId, e.StartUtc, e.Status });
    }
}
