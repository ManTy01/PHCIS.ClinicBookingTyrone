using ClinicBooking.Application.Clinics;
using ClinicBooking.Application.Slots;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

/// <summary>
/// Public clinic list and week grid. <see cref="AllowAnonymous"/>: no JWT.
/// Booked cells come back as status only — never patient names.
/// sealed: leaf controller.
/// </summary>
[ApiController]
[Route("api/clinics")]
[AllowAnonymous]
public sealed class ClinicsController(IMediator mediator) : ControllerBase
{
    /// <summary>Seeded clinics (Soweto + Sandton) for the home picker.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClinicDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetClinicsQuery(), cancellationToken));
    }

    /// <summary>
    /// Public schedule. <paramref name="date"/> is one day; <paramref name="from"/>/<paramref name="to"/> is a range (inclusive).
    /// Returns Available, Booked, and Blocked. No patient names.
    /// </summary>
    [HttpGet("{clinicId:guid}/slots")]
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
}
