using ClinicBooking.Domain.Enums;

namespace ClinicBooking.Application.Abstractions;

/// <summary>
/// PDF optional extra: SignalR.
/// Application handlers call this after a successful SaveChanges.
/// They do not know about hubs or WebSockets — that stays in ClinicBooking.Api
/// (SignalRSlotBoardNotifier). Same pattern as IJwtTokenService / IUserDirectory.
///
/// Payload is clinic + slot + status only. Never a patient name, so the public
/// board can subscribe without a JWT.
/// </summary>
public interface ISlotBoardNotifier
{
    /// <summary>
    /// Book → Booked, cancel/unblock → Available, block → Blocked.
    /// Reschedule calls this twice (old slot Available, new slot Booked).
    /// </summary>
    Task NotifySlotChangedAsync(
        Guid clinicId,
        Guid timeSlotId,
        TimeSlotStatus status,
        CancellationToken cancellationToken = default);
}
