using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ClinicBooking.Api.Hubs;

/// <summary>
/// Optional extra: SignalR real-time slot board.
///
/// Why a Hub (not a controller): browsers keep a WebSocket open. The server can push
/// "this cell changed" without the UI polling GET /slots every second.
///
/// Why [AllowAnonymous] (no JWT on this hub):
/// The public week grid is already anonymous HTTP. The SignalR payload is only
/// clinicId + slotId + status — never a patient name. Booking still requires JWT
/// on POST /api/appointments. Staff diary names come from a later HTTP GET with JWT.
///
/// Why groups: JoinClinic puts this connection into "clinic:{guid}". Notify then
/// sends to that group only — Cpt does not refresh when clinic is booked.
///
/// sealed: hubs are leaf types; we never inherit from this hub.
/// </summary>
[AllowAnonymous]
public sealed class SlotBoardHub : Hub
{
    /// <summary>
    /// WASM calls this after HubConnection.StartAsync.
    /// Context.ConnectionId is this browser tab's socket. Groups is SignalR's mailing list.
    /// </summary>
    public Task JoinClinic(Guid clinicId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(clinicId));

    /// <summary>
    /// Called when the user opens a different clinic, or the page unloads (StopWatchingAsync).
    /// Without this, the old connection would keep getting the previous clinic's events.
    /// </summary>
    public Task LeaveClinic(Guid clinicId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(clinicId));

    /// <summary>
    /// Shared with SignalRSlotBoardNotifier so send and join use the same string.
    /// :N = guid with no dashes, stable and URL-safe.
    /// </summary>
    public static string GroupName(Guid clinicId) => $"clinic:{clinicId:N}";
}
