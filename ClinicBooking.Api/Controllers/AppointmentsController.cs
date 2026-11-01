using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClinicBooking.Application.Appointments;
using ClinicBooking.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

/// <summary>
/// Patient booking surface. <see cref="AuthorizeAttribute"/> + role Patient means a JWT
/// with that role claim is required (PDF: JWT Tokens for secure API access).
/// Guests see 401 until they login. Staff hitting this get 403 (wrong role).
/// sealed: not a base class.
/// </summary>
[ApiController]
[Route("api/appointments")]
[Authorize(Roles = Roles.Patient)]
public sealed class AppointmentsController(IMediator mediator) : ControllerBase
{
    /// <summary>My appointments for the user id inside the JWT (sub / NameIdentifier).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> Mine(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetMyAppointmentsQuery(CurrentUserId()), cancellationToken));
    }

    /// <summary>Book a slot. Handler re-checks availability; unique index is the race backup → 409.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookAppointmentResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Book([FromBody] BookRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new BookAppointmentCommand(CurrentUserId(), request.TimeSlotId),
            cancellationToken);
        return Created($"/api/appointments/{result.AppointmentId}", result);
    }

    /// <summary>Cancel own booking and free the slot. Body reason is optional.</summary>
    [HttpPost("{appointmentId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(
        Guid appointmentId,
        [FromBody] CancelRequest? request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new CancelAppointmentCommand(CurrentUserId(), appointmentId, request?.Reason),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Keep the old row as Rescheduled and insert a new Booked row on another slot.</summary>
    [HttpPost("{appointmentId:guid}/reschedule")]
    [ProducesResponseType(typeof(BookAppointmentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookAppointmentResult>> Reschedule(
        Guid appointmentId,
        [FromBody] RescheduleRequest request,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new RescheduleAppointmentCommand(CurrentUserId(), appointmentId, request.NewTimeSlotId),
            cancellationToken);
    }

    /// <summary>
    /// Identity user id from the JWT. We write both short names (sub) and ClaimTypes
    /// so this still works if MapInboundClaims is on or off.
    /// </summary>
    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("Missing user id claim.");
}

/// <summary>POST /api/appointments body.</summary>
public sealed record BookRequest(Guid TimeSlotId);

/// <summary>Optional cancel reason stored on the appointment row.</summary>
public sealed record CancelRequest(string? Reason);

/// <summary>POST .../reschedule body — the free slot to move to.</summary>
public sealed record RescheduleRequest(Guid NewTimeSlotId);
