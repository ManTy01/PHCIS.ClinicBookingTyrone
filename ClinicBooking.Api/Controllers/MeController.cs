using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClinicBooking.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

/// <summary>
/// Signed-in user's own details. Any authenticated role (Patient, Staff, Admin).
/// <see cref="AuthorizeAttribute"/> with no role = valid JWT required (PDF: JWT).
/// sealed: leaf controller.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IMediator mediator) : ControllerBase
{
    /// <summary>Load profile. Patients get name/phone; staff/admin only email + roles.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetMyProfileQuery(CurrentUserId()), cancellationToken);
    }

    /// <summary>Update own details and return a fresh JWT (email claim may have changed).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResult>> Update(
        [FromBody] UpdateMyProfileRequest request,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new UpdateMyProfileCommand(
                CurrentUserId(),
                request.Email,
                request.FirstName,
                request.LastName,
                request.Phone,
                request.DateOfBirth),
            cancellationToken);
    }

    /// <summary>User id from the JWT - same claim fallback as AppointmentsController.</summary>
    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("Missing user id claim.");
}

/// <summary>PUT /api/me body. UserId comes from the JWT, not the client.</summary>
public sealed record UpdateMyProfileRequest(
    string Email,
    string? FirstName,
    string? LastName,
    string? Phone,
    DateOnly? DateOfBirth);
