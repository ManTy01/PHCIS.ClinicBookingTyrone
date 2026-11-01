using Microsoft.JSInterop;

namespace ClinicBooking.Blazor.Auth;

/// <summary>
/// PDF JWT: the WASM app stores the token in localStorage (no cookies).
/// Blazor never talks to SQL — this token is the only secret it holds.
/// sealed: one session object per browser tab (registered Singleton in Program.cs).
/// </summary>
public sealed class AuthSession(IJSRuntime js)
{
    public const string StorageKey = "phcis.jwt";

    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Token);

    /// <summary>Called from App.razor on first load so AuthorizeView sees the saved JWT.</summary>
    public async Task LoadAsync()
    {
        Token = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        Email = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey + ".email");
    }

    /// <summary>After login/register/profile update — persist token so refresh keeps the user signed in.</summary>
    public async Task SetAsync(string token, string email)
    {
        Token = token;
        Email = email;
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, token);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey + ".email", email);
    }

    /// <summary>Sign out: drop memory + localStorage. Next API call has no Bearer header → 401.</summary>
    public async Task ClearAsync()
    {
        Token = null;
        Email = null;
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey + ".email");
    }
}
