namespace ClinicBooking.Blazor.Scheduling;

/// <summary>Clinics are seeded on South Africa Standard Time (UTC+2, no DST). WASM has no TimeZoneInfo for Windows TZ ids.</summary>
public static class SastSchedule
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    /// <summary>Convert stored UTC to SAST for the grid labels.</summary>
    public static DateTimeOffset ToSast(DateTimeOffset utc) => utc.ToOffset(Offset);

    /// <summary>Calendar date in SAST (used as the grid column key).</summary>
    public static DateOnly ToSastDate(DateTimeOffset utc) =>
        DateOnly.FromDateTime(ToSast(utc).DateTime);

    /// <summary>Ignore seconds so 08:00:00.123 still matches the 08:00 row.</summary>
    public static TimeSpan TruncateToMinute(TimeSpan time) => new(time.Hours, time.Minutes, 0);

    /// <summary>If today is Sat/Sun, jump to next Monday. Otherwise this week's Monday (unless the week is already past Friday).</summary>
    public static DateOnly UpcomingWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monday = StartOfWeek(today);
        return monday.AddDays(4) < today ? monday.AddDays(7) : monday;
    }

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var delta = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-delta);
    }

    /// <summary>Human label on the confirmation page and book bar.</summary>
    public static string Format(DateTimeOffset utc) =>
        ToSast(utc).ToString("ddd d MMM yyyy HH:mm");
}
