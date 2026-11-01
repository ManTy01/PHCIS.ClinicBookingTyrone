using System.Text.Json;
using ClinicBooking.Blazor.Models;
using Microsoft.JSInterop;

namespace ClinicBooking.Blazor.Auth;

/// <summary>
/// Guest picks a slot then signs in. sessionStorage (not localStorage) so closing the tab drops it.
/// The API does not hold the slot during register — first JWT book wins, 409 if taken.
/// sealed: one store per circuit/scope.
/// </summary>
public sealed class PendingBookingStore(IJSRuntime js)
{
    public const string StorageKey = "phcis.pendingBooking";

    /// <summary>Called from Slots.razor before NavigateTo("/login").</summary>
    public async Task SaveAsync(PendingBooking booking)
    {
        await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, JsonSerializer.Serialize(booking));
    }

    /// <summary>Called after login. Null means they signed in without a pending slot.</summary>
    public async Task<PendingBooking?> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PendingBooking>(json);
    }

    /// <summary>Always clear after success or 409 so we do not book again on the next login.</summary>
    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
    }
}
