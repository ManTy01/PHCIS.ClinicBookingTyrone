using ClinicBooking.Application.Slots;
using ClinicBooking.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

/// <summary>
/// Clinic staff diary. JWT must carry ClinicStaff or Admin (PDF: JWT + Identity roles).
/// Patient names stay off the public /slots board and only appear here.
/// sealed: leaf controller.
/// </summary>
[ApiController]
[Route("api/staff")]
[Authorize(Roles = $"{Roles.ClinicStaff},{Roles.Admin}")]
public sealed class StaffController(IMediator mediator) : ControllerBase
{
    /// <summary>Week grid for one clinic including who booked each hour.</summary>
    [HttpGet("clinics/{clinicId:guid}/diary")]
    public async Task<ActionResult<IReadOnlyList<DiarySlotDto>>> Diary(
        Guid clinicId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.Today);
        var end = to ?? start.AddDays(4);
        return Ok(await mediator.Send(new GetClinicDiaryQuery(clinicId, start, end), cancellationToken));
    }

    /// <summary>Take a free hour off the public board (lunch / closed). Cannot block a booked slot. 200 + message (not 204).</summary>
    [HttpPost("slots/{slotId:guid}/block")]
    [ProducesResponseType(typeof(SlotActionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SlotActionResult>> Block(Guid slotId, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new BlockTimeSlotCommand(slotId), cancellationToken));
    }

    /// <summary>Put a blocked hour back to Available. 200 + message (not 204).</summary>
    [HttpPost("slots/{slotId:guid}/unblock")]
    [ProducesResponseType(typeof(SlotActionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SlotActionResult>> Unblock(Guid slotId, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new UnblockTimeSlotCommand(slotId), cancellationToken));
    }
}
