using ClinicBooking.Application.Clinics;
using ClinicBooking.Domain;
using ClinicBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBooking.Infrastructure.Persistence;

/// <summary>
/// Demo users + weekday slot grid. Clinics themselves are HasData in ClinicConfiguration.
/// Password for every seed user: Password123!
/// </summary>
public static class DatabaseSeeder
{
    public const string SeedPassword = "Password123!";

    /// <summary>Idempotent — safe to run on every Development / Docker API start.</summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var role in new[] { Roles.Patient, Roles.ClinicStaff, Roles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, db, "admin@phcis.local", Roles.Admin, "Clinic", "Admin", cancellationToken);
        await EnsureUserAsync(userManager, db, "staff@phcis.local", Roles.ClinicStaff, "Jane", "Doe", cancellationToken);
        await EnsureUserAsync(userManager, db, "patient@phcis.local", Roles.Patient, "John", "Doe", cancellationToken);

        await EnsureHourSlotsAsync(db, cancellationToken);
    }

    /// <summary>Create the Identity login if missing. Patient role also gets a Patients row.</summary>
    private static async Task EnsureUserAsync(
        UserManager<IdentityUser> userManager,
        AppDbContext db,
        string email,
        string role,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, SeedPassword);
            await userManager.AddToRoleAsync(user, role);
        }

        if (role == Roles.Patient && !await db.Patients.AnyAsync(p => p.UserId == user.Id, cancellationToken))
        {
            db.Patients.Add(new Patient
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = email
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>If slots are missing or still 30-minute, wipe and seed 08:00–16:00 one-hour weekdays.</summary>
    private static async Task EnsureHourSlotsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var sample = await db.TimeSlots.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var needsHourSlots = sample is null || sample.EndUtc - sample.StartUtc != TimeSpan.FromHours(1);
        if (!needsHourSlots)
        {
            return;
        }

        db.Appointments.RemoveRange(await db.Appointments.ToListAsync(cancellationToken));
        db.TimeSlots.RemoveRange(await db.TimeSlots.ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);

        var clinics = await db.Clinics.ToListAsync(cancellationToken);
        foreach (var clinic in clinics)
        {
            db.TimeSlots.AddRange(ClinicHourSlots.BuildWeekdayHours(clinic.Id, clinic.TimeZoneId));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
