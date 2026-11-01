using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Slots;

/// <summary>
/// Public board: times and status only. Never patient names.
/// Booked/Blocked rows stay visible so the grid can disable those cells.
/// </summary>
public sealed record TimeSlotDto(Guid Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, TimeSlotStatus Status);

/// <summary>PDF: time slot availability. From/To are clinic-local calendar dates.</summary>
public sealed record GetAvailableSlotsQuery(Guid ClinicId, DateOnly From, DateOnly To)
    : IRequest<IReadOnlyList<TimeSlotDto>>;

/// <summary>Cap the range so a client cannot ask for a year of slots in one call.</summary>
public sealed class GetAvailableSlotsQueryValidator : AbstractValidator<GetAvailableSlotsQuery>
{
    public GetAvailableSlotsQueryValidator()
    {
        RuleFor(x => x.ClinicId).NotEmpty();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 14)
            .WithMessage("Schedule range cannot exceed 14 days.");
    }
}

/// <summary>
/// Converts clinic-local dates to UTC using the clinic time zone, then projects via AutoMapper (PDF).
/// sealed: leaf handler.
/// </summary>
public sealed class GetAvailableSlotsQueryHandler(IAppDbContext db, IMapper mapper)
    : IRequestHandler<GetAvailableSlotsQuery, IReadOnlyList<TimeSlotDto>>
{
    /// <summary>Inclusive From, exclusive next-day To. Unknown clinic → 404.</summary>
    public async Task<IReadOnlyList<TimeSlotDto>> Handle(
        GetAvailableSlotsQuery request,
        CancellationToken cancellationToken)
    {
        var clinic = await db.Clinics.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ClinicId, cancellationToken)
            ?? throw new NotFoundException("Clinic not found.");

        var tz = ResolveTimeZone(clinic.TimeZoneId);
        var localStart = new DateTime(request.From.Year, request.From.Month, request.From.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = new DateTime(request.To.Year, request.To.Month, request.To.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

        return await db.TimeSlots
            .AsNoTracking()
            .Where(s =>
                s.ClinicId == request.ClinicId
                && s.StartUtc >= startUtc
                && s.StartUtc < endUtc)
            .OrderBy(s => s.StartUtc)
            .ProjectTo<TimeSlotDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Windows TZ on LocalDB; IANA id on Linux Docker if Windows id is missing.</summary>
    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        }
    }
}
