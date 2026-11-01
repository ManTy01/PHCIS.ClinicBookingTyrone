using ClinicBooking.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

/// <summary>
/// Login and register. <see cref="AllowAnonymous"/>: these endpoints mint the JWT,
/// so they cannot require a token.
/// sealed: controllers are leaf types — we never inherit from them.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>Creates Identity user + Patient row, returns JWT (PDF: Identity + JWT).</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterPatientCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Created(string.Empty, result);
    }

    /// <summary>Checks email/password via Identity, then returns a signed JWT for Blazor to store.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResult>> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        return await mediator.Send(command, cancellationToken);
    }
}
