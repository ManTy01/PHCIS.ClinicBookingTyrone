using ClinicBooking.Domain;
using ClinicBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicBooking.Infrastructure.Configuration;

/// <summary>Maps Clinic. Unique name. Seeds Soweto + Sandton so GET /api/clinics is never empty after migrate. sealed: EF config, not inherited.</summary>
public sealed class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    /// <summary>HasData uses SeedIds so tests and the UI can hard-code the two clinic GUIDs.</summary>
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.ToTable("Clinics");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(32);
        builder.Property(e => e.TimeZoneId).HasMaxLength(64).IsRequired();

        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new Clinic
            {
                Id = SeedIds.SowetoClinic,
                Name = "Soweto Community Clinic",
                Address = "Diepkloof, Soweto",
                Phone = "011 000 1001",
                TimeZoneId = "South Africa Standard Time"
            },
            new Clinic
            {
                Id = SeedIds.SandtonClinic,
                Name = "Sandton Primary Care",
                Address = "Rivonia Road, Sandton",
                Phone = "011 000 1002",
                TimeZoneId = "South Africa Standard Time"
            });
    }
}
