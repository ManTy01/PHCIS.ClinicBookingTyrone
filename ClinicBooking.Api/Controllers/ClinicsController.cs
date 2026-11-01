using ClinicBooking.Application.Clinics;
using ClinicBooking.Application.Slots;
using ClinicBooking.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

/// <summary>
/// Public clinic list and week grid. Writes are Admin-only (PDF: JWT + Identity roles).
/// GET stays anonymous. POST/PUT require a JWT with the Admin role — staff/patient get 403 Not authorised.
/// sealed: leaf controller.
/// </summary>
[ApiController]
[Route("api/clinics")]
public sealed class ClinicsController(IMediator mediator) : ControllerBase
{
    /// <summary>Seeded clinics plus any admin-added rows, for the home picker.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ClinicDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetClinicsQuery(), cancellationToken));
    }

    /// <summary>
    /// Public schedule. <paramref name="date"/> is one day; <paramref name="from"/>/<paramref name="to"/> is a range (inclusive).
    /// Returns Available, Booked, and Blocked. No patient names.
    /// </summary>
    [HttpGet("{clinicId:guid}/slots")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TimeSlotDto>>> GetSlots(
        Guid clinicId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var start = from ?? date ?? DateOnly.FromDateTime(DateTime.Today);
        var end = to ?? date ?? start;
        return Ok(await mediator.Send(new GetAvailableSlotsQuery(clinicId, start, end), cancellationToken));
    }

    /// <summary>Admin JWT required. Creates the clinic and seeds weekday hour slots so View schedule is not empty.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ClinicDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ClinicWriteRequest request, CancellationToken cancellationToken)
    {
        var created = await mediator.Send(
            new CreateClinicCommand(request.Name, request.Address, request.Phone),
            cancellationToken);
        return Created($"/api/clinics/{created.Id}", created);
    }

    /// <summary>Admin JWT required. Name/address/phone only — does not rebuild slots.</summary>
    [HttpPut("{clinicId:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ClinicDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClinicDto>> Update(
        Guid clinicId,
        [FromBody] ClinicWriteRequest request,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new UpdateClinicCommand(clinicId, request.Name, request.Address, request.Phone),
            cancellationToken);
    }
}

/// <summary>POST/PUT /api/clinics body. Time zone is SAST on the server, not the client.</summary>
public sealed record ClinicWriteRequest(string Name, string Address, string? Phone);
