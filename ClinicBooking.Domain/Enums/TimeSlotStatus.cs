namespace ClinicBooking.Domain.Enums;

/// <summary>
/// A slot is a bookable window at a clinic.
/// Available = patient can take it.
/// Booked = an appointment owns it.
/// Blocked = staff took it off the board (lunch, training, closed).
/// Stored as int in SQL; JSON uses the name via JsonStringEnumConverter.
/// </summary>
public enum TimeSlotStatus
{
    Available = 1,
    Booked = 2,
    Blocked = 3
}
