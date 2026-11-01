using ClinicBooking.Api.Hubs;
using ClinicBooking.Application.Abstractions;
using ClinicBooking.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace ClinicBooking.Api.Realtime;

/// <summary>
/// Application handlers cannot reference SignalR (that would pull HTTP into CQRS).
/// They call ISlotBoardNotifier; this class is the API adapter.
///
/// IHubContext&lt;SlotBoardHub&gt; lets us push from a MediatR handler — we are not
/// inside a hub method, we are after SaveChanges in Book/Cancel/Reschedule/Block.
///
/// Event name "SlotChanged" must match Blazor: connection.On&lt;SlotBoardChange&gt;("SlotChanged", ...).
/// Status is a string ("Booked") so WASM JSON does not depend on enum numbers.
///
/// Try/catch: a dropped socket must not turn a successful book into HTTP 500.
/// sealed: one implementation registered in Program.cs.
/// </summary>
public sealed class SignalRSlotBoardNotifier(
    IHubContext<SlotBoardHub> hub,
    ILogger<SignalRSlotBoardNotifier> log) : ISlotBoardNotifier
{
    /// <summary>
    /// Clients.Group(...) = only tabs that called JoinClinic for this clinic.
    /// Anonymous object serializes as camelCase: clinicId, timeSlotId, status.
    /// </summary>
    public async Task NotifySlotChangedAsync(
        Guid clinicId,
        Guid timeSlotId,
        TimeSlotStatus status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients
                .Group(SlotBoardHub.GroupName(clinicId))
                .SendAsync(
                    "SlotChanged",
                    new { clinicId, timeSlotId, status = status.ToString() },
                    cancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "SignalR notify failed for clinic {ClinicId} slot {SlotId}", clinicId, timeSlotId);
        }
    }
}
