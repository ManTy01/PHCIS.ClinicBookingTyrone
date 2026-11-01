using ClinicBooking.Domain.Enums;

namespace ClinicBooking.Domain.Entities;

/// <summary>
/// A booking of one patient onto one time slot.
/// ConfirmationCode is what the patient sees after a successful book
/// (e.g. PHC-A1B2C3). Reschedule does not UPDATE this row — it marks Rescheduled
/// and inserts a new Booked row pointing PreviousAppointmentId here.
/// Not sealed: EF entity.
/// </summary>
public class Appointment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid ClinicId { get; set; }
    public Clinic Clinic { get; set; } = null!;

    public Guid TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

    /// <summary>Short code shown on the confirmation screen. Unique in SQL.</summary>
    public required string ConfirmationCode { get; set; }

    public DateTimeOffset BookedAtUtc { get; set; }

    /// <summary>Filled when this booking replaced an earlier one.</summary>
    public Guid? PreviousAppointmentId { get; set; }
    public Appointment? PreviousAppointment { get; set; }

    public string? CancellationReason { get; set; }
}
