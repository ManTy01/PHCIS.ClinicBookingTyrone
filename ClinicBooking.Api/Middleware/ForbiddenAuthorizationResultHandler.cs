using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Middleware;

/// <summary>
/// JWT [Authorize(Roles = …)] with the wrong role is 403 with an empty body by default.
/// Write ProblemDetails so Blazor (and Swagger) can show "Not authorised".
/// 401 (no token) still goes through the default challenge.
/// sealed: one handler registered in Program.cs.
/// </summary>
public sealed class ForbiddenAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _inner = new();

    /// <summary>Called after [Authorize] is evaluated, before the action runs.</summary>
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Not authorised",
                Detail = "Not authorised",
                Status = StatusCodes.Status403Forbidden
            });
            return;
        }

        await _inner.HandleAsync(next, context, policy, authorizeResult);
    }
}
