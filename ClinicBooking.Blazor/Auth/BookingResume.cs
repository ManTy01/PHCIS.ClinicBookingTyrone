using System.Net;
using ClinicBooking.Blazor.Api;
using Microsoft.AspNetCore.Components;

namespace ClinicBooking.Blazor.Auth;

/// <summary>
/// After login/register: POST the saved slot with the new JWT, then confirmation page.
/// If someone else took it during sign-up, API returns 409 and we send them back to the grid.
/// sealed: orchestration helper, not inherited.
/// </summary>
public sealed class BookingResume(PendingBookingStore store, ClinicApiClient api, NavigationManager navigation)
{
    /// <summary>True if we handled a pending booking (success or taken). False = just go home/returnUrl.</summary>
    public async Task<bool> TryCompleteAsync()
    {
        var pending = await store.LoadAsync();
        if (pending is null)
        {
            return false;
        }

        try
        {
            var result = await api.BookAsync(pending.TimeSlotId);
            await store.ClearAsync();
            navigation.NavigateTo(
                $"/booking-confirmed?code={Uri.EscapeDataString(result.ConfirmationCode)}&start={Uri.EscapeDataString(result.StartUtc.ToString("o"))}");
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await store.ClearAsync();
            navigation.NavigateTo($"/clinics/{pending.ClinicId}/slots?taken=1");
            return true;
        }
    }

    /// <summary>Login/Register pages call this. Pending book wins over returnUrl.</summary>
    public async Task CompleteOrContinueAsync(string? returnUrl)
    {
        if (await TryCompleteAsync())
        {
            return;
        }

        navigation.NavigateTo(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }
}
