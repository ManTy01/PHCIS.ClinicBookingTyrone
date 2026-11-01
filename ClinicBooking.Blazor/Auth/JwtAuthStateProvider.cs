using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace ClinicBooking.Blazor.Auth;

/// <summary>
/// Tells Blazor AuthorizeView / [Authorize] who is signed in by reading the JWT payload.
/// We do not validate the signature here — the API does that. This is display/routing only.
/// sealed: one provider registered as AuthenticationStateProvider.
/// </summary>
public sealed class JwtAuthStateProvider(AuthSession session) : AuthenticationStateProvider
{
    /// <summary>Empty identity when no token. Otherwise claims from the JWT (sub, email, role).</summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!session.IsLoggedIn)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var identity = new ClaimsIdentity(ParseClaims(session.Token!), "jwt");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    /// <summary>Call after login/logout so the header and nav re-render.</summary>
    public void Notify() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    /// <summary>JWT is header.payload.signature — we only decode payload (middle part) as JSON claims.</summary>
    private static IEnumerable<Claim> ParseClaims(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var json = Encoding.UTF8.GetString(PadBase64(payload));
        using var doc = JsonDocument.Parse(json);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var type = property.Name switch
            {
                "sub" or "nameid" => ClaimTypes.NameIdentifier,
                "email" => ClaimTypes.Email,
                "role" or "roles" => ClaimTypes.Role,
                _ => property.Name
            };

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    yield return new Claim(type, item.GetString() ?? string.Empty);
                }
            }
            else
            {
                yield return new Claim(type, property.Value.ToString());
            }
        }
    }

    /// <summary>JWT uses URL-safe base64 without padding. Convert.FromBase64String needs + / and =.</summary>
    private static byte[] PadBase64(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
