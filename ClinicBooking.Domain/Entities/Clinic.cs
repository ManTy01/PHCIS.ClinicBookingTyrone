namespace ClinicBooking.Domain.Entities;

/// <summary>
/// A physical clinic patients book against. TimeZoneId is Windows TZ
/// (e.g. "South Africa Standard Time") so slot display is local, storage is UTC.
/// Not sealed: EF entity — proxies / tests may subclass.
/// </summary>
public class Clinic
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public string? Phone { get; set; }

    /// <summary>Windows time-zone id used when converting "today" into UTC slot queries.</summary>
    public required string TimeZoneId { get; set; }

    public ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
