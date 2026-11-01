namespace ClinicBooking.Domain.Enums;

/// <summary>
/// Lifecycle of an appointment row. We do not delete bookings — cancel/reschedule
/// keep history (same idea as OZ keeping ingest rows instead of updating them).
/// </summary>
public enum AppointmentStatus
{
    Booked = 1,
    Cancelled = 2,
    Rescheduled = 3
}
