namespace ClinicBooking.Blazor.Models;

/// <summary>
/// Saved in sessionStorage so a guest can log in / register without losing the slot they clicked.
/// sealed record: immutable DTO, not inherited.
/// </summary>
public sealed record PendingBooking(Guid ClinicId, Guid TimeSlotId, DateOnly Date);
