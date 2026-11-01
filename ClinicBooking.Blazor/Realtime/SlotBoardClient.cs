using ClinicBooking.Blazor.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace ClinicBooking.Blazor.Realtime;

/// <summary>
/// PDF SignalR client for Blazor WASM. HTTP still does booking; this socket only listens.
///
/// Flow:
/// 1) WatchClinicAsync starts HubConnection to {ApiBaseUrl}/hubs/slots (same origin rules as CORS).
/// 2) Invoke JoinClinic so we only hear that clinic.
/// 3) On "SlotChanged" run the page callback (reload the week grid).
/// 4) StopWatchingAsync on page dispose so we do not leak the group.
///
/// JWT is not sent on this socket (hub is AllowAnonymous). Staff names are loaded
/// afterwards with ClinicApiClient + Bearer token.
///
/// sealed: one client per circuit (scoped in Program.cs). Not a base class.
/// </summary>
public sealed class SlotBoardClient(IConfiguration config) : IAsyncDisposable
{
    private HubConnection? _connection;
    private Guid? _clinicId;
    private Func<SlotBoardChange, Task>? _onChanged;

    /// <summary>
    /// Pages call this in OnParametersSetAsync. Reuses the same socket if already started.
    /// Switching clinics leaves the old group first.
    /// </summary>
    public async Task WatchClinicAsync(Guid clinicId, Func<SlotBoardChange, Task> onChanged)
    {
        _onChanged = onChanged;
        await EnsureConnectionAsync();

        if (_clinicId is Guid previous && previous != clinicId)
        {
            await _connection!.InvokeAsync("LeaveClinic", previous);
        }

        _clinicId = clinicId;
        if (_connection!.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("JoinClinic", clinicId);
        }
    }

    /// <summary>
    /// Slots.razor / StaffDiary DisposeAsync. Clears the callback so a late event
    /// does not touch a disposed page. Leaves the SignalR group.
    /// </summary>
    public async Task StopWatchingAsync()
    {
        _onChanged = null;
        if (_connection is { State: HubConnectionState.Connected } && _clinicId is Guid id)
        {
            await _connection.InvokeAsync("LeaveClinic", id);
        }

        _clinicId = null;
    }

    /// <summary>
    /// Builds the HubConnection once per circuit. WithAutomaticReconnect retries the WebSocket.
    /// After reconnect the ConnectionId is new — groups are empty — so Reconnected re-joins.
    /// </summary>
    private async Task EnsureConnectionAsync()
    {
        if (_connection is not null)
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
            }

            return;
        }

        var baseUrl = (config["ApiBaseUrl"] ?? throw new InvalidOperationException("ApiBaseUrl is not configured."))
            .TrimEnd('/');

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/slots")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<SlotBoardChange>("SlotChanged", async change =>
        {
            if (_onChanged is null)
            {
                return;
            }

            await _onChanged(change);
        });

        _connection.Reconnected += async _ =>
        {
            if (_clinicId is Guid id)
            {
                await _connection.InvokeAsync("JoinClinic", id);
            }
        };

        await _connection.StartAsync();
    }

    /// <summary>DI may dispose the scoped client when the WASM circuit ends.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
