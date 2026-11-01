using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Slots;

/// <summary>API body for block/unblock. 200 instead of 204 so Swagger/Postman show a message.</summary>
public sealed record SlotActionResult(string Message);

/// <summary>Staff takes a free hour off the public board (training, closed).</summary>
public sealed record BlockTimeSlotCommand(Guid TimeSlotId) : IRequest<SlotActionResult>;

/// <summary>PDF FluentValidation.</summary>
public sealed class BlockTimeSlotCommandValidator : AbstractValidator<BlockTimeSlotCommand>
{
    public BlockTimeSlotCommandValidator()
    {
        RuleFor(x => x.TimeSlotId).NotEmpty();
    }
}

/// <summary>Cannot block a booked slot — cancel the appointment first. sealed: leaf handler.</summary>
public sealed class BlockTimeSlotCommandHandler(IAppDbContext db, ISlotBoardNotifier board)
    : IRequestHandler<BlockTimeSlotCommand, SlotActionResult>
{
    /// <summary>Available or already Blocked can become Blocked. Booked → 409.</summary>
    public async Task<SlotActionResult> Handle(BlockTimeSlotCommand request, CancellationToken cancellationToken)
    {
        var slot = await db.TimeSlots
            .FirstOrDefaultAsync(s => s.Id == request.TimeSlotId, cancellationToken)
            ?? throw new NotFoundException("Time slot not found.");

        if (slot.Status == TimeSlotStatus.Booked)
        {
            throw new ConflictException("A booked slot cannot be blocked. Cancel the appointment first.");
        }

        slot.Status = TimeSlotStatus.Blocked;
        await db.SaveChangesAsync(cancellationToken);
        await board.NotifySlotChangedAsync(slot.ClinicId, slot.Id, TimeSlotStatus.Blocked, cancellationToken);
        return new SlotActionResult("That hour is now blocked on the public board.");
    }
}

/// <summary>Put a blocked hour back on the public board.</summary>
public sealed record UnblockTimeSlotCommand(Guid TimeSlotId) : IRequest<SlotActionResult>;

/// <summary>PDF FluentValidation.</summary>
public sealed class UnblockTimeSlotCommandValidator : AbstractValidator<UnblockTimeSlotCommand>
{
    public UnblockTimeSlotCommandValidator()
    {
        RuleFor(x => x.TimeSlotId).NotEmpty();
    }
}

/// <summary>Only Blocked → Available. Unblocking Booked would steal a patient's hour. sealed: leaf handler.</summary>
public sealed class UnblockTimeSlotCommandHandler(IAppDbContext db, ISlotBoardNotifier board)
    : IRequestHandler<UnblockTimeSlotCommand, SlotActionResult>
{
    /// <summary>Wrong status → 409 so staff know they clicked the wrong cell.</summary>
    public async Task<SlotActionResult> Handle(UnblockTimeSlotCommand request, CancellationToken cancellationToken)
    {
        var slot = await db.TimeSlots
            .FirstOrDefaultAsync(s => s.Id == request.TimeSlotId, cancellationToken)
            ?? throw new NotFoundException("Time slot not found.");

        if (slot.Status != TimeSlotStatus.Blocked)
        {
            throw new ConflictException("Only a blocked slot can be unblocked.");
        }

        slot.Status = TimeSlotStatus.Available;
        await db.SaveChangesAsync(cancellationToken);
        await board.NotifySlotChangedAsync(slot.ClinicId, slot.Id, TimeSlotStatus.Available, cancellationToken);
        return new SlotActionResult("That hour is available to book again.");
    }
}
