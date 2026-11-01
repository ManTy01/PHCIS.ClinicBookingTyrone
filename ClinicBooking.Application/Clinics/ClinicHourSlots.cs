using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Enums;

namespace ClinicBooking.Application.Clinics;

/// <summary>
/// Shared 08:00–16:00 weekday grid. The seeder uses this for Jhb/Cpt;
/// CreateClinicCommand uses it so an admin-added clinic is bookable immediately.
/// sealed is not used — static helper, not a type we inherit from.
/// </summary>
public static class ClinicHourSlots
{
    /// <summary>Windows TZ id stored on Clinic.TimeZoneId (IANA fallback is in ResolveTimeZone).</summary>
    public const string DefaultTimeZoneId = "South Africa Standard Time";

    /// <summary>Next 10 weekdays, 8 hour cells each, stored as UTC with clinic-local start.</summary>
    public static IReadOnlyList<TimeSlot> BuildWeekdayHours(Guid clinicId, string timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        var slots = new List<TimeSlot>();
        var added = 0;
        for (var day = 0; added < 10 && day < 21; day++)
        {
            var date = localToday.AddDays(day);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            added++;
            for (var hour = 8; hour < 16; hour++)
            {
                var localStart = new DateTime(date.Year, date.Month, date.Day, hour, 0, 0, DateTimeKind.Unspecified);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
                slots.Add(new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    ClinicId = clinicId,
                    StartUtc = new DateTimeOffset(startUtc, TimeSpan.Zero),
                    EndUtc = new DateTimeOffset(startUtc.AddHours(1), TimeSpan.Zero),
                    Status = TimeSlotStatus.Available,
                    Capacity = 1
                });
            }
        }

        return slots;
    }

    /// <summary>Windows TZ on LocalDB; Africa/Johannesburg if the API is in Linux Docker.</summary>
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        }
    }
}
