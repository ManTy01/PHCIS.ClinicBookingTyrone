using ClinicBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicBooking.Infrastructure.Configuration;

/// <summary>Patient profile row. UserId is the Identity id from the JWT sub claim. sealed: EF config.</summary>
public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    /// <summary>Unique UserId (1:1 with Identity) and unique Email.</summary>
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(32);

        builder.HasIndex(e => e.UserId).IsUnique();
        builder.HasIndex(e => e.Email).IsUnique();
    }
}
