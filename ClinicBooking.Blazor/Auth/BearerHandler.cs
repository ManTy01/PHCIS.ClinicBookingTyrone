using System.Net.Http.Headers;

namespace ClinicBooking.Blazor.Auth;

/// <summary>
/// PDF JWT: attaches Authorization: Bearer &lt;token&gt; to every HttpClient call.
/// DelegatingHandler runs inside HttpClient's pipeline (registered in Program.cs).
/// sealed: not a base class.
/// </summary>
public sealed class BearerHandler(AuthSession session) : DelegatingHandler
{
    /// <summary>Skip the header when logged out so public GET /api/clinics stays anonymous.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (session.IsLoggedIn)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
