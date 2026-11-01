using ClinicBooking.Domain.Enums;

namespace ClinicBooking.Domain.Entities;

/// <summary>
/// One bookable window at a clinic (e.g. 09:00–10:00).
/// Capacity is 1 for this assessment: one patient per slot.
/// Unique (ClinicId, StartUtc) in EF is the "two generators cannot create 09:00 twice" lock.
/// Not sealed: EF entity.
/// </summary>
public class TimeSlot
{
    public Guid Id { get; set; }
    public Guid ClinicId { get; set; }
    public Clinic Clinic { get; set; } = null!;

    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public TimeSlotStatus Status { get; set; } = TimeSlotStatus.Available;

    /// <summary>How many patients this slot can take. Demo uses 1.</summary>
    public int Capacity { get; set; } = 1;

    /// <summary>Set when Status is Booked. One active appointment per slot.</summary>
    public Appointment? Appointment { get; set; }
}
