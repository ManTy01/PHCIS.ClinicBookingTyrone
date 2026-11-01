using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Slots;

/// <summary>
/// Staff board for one clinic. Booked cells include the patient name.
/// Public /slots never returns this.
/// </summary>
public sealed record DiarySlotDto(
    Guid Id,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    TimeSlotStatus Status,
    string? PatientName,
    string? ConfirmationCode,
    Guid? AppointmentId);

/// <summary>Staff week query. Same date window as the public board, plus PII.</summary>
public sealed record GetClinicDiaryQuery(Guid ClinicId, DateOnly From, DateOnly To)
    : IRequest<IReadOnlyList<DiarySlotDto>>;

/// <summary>Same 14-day cap as the public slots query.</summary>
public sealed class GetClinicDiaryQueryValidator : AbstractValidator<GetClinicDiaryQuery>
{
    public GetClinicDiaryQueryValidator()
    {
        RuleFor(x => x.ClinicId).NotEmpty();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 14)
            .WithMessage("Diary range cannot exceed 14 days.");
    }
}

/// <summary>
/// Two queries: slots, then booked appointments with names. Public /slots never joins Patient.
/// sealed: leaf handler.
/// </summary>
public sealed class GetClinicDiaryQueryHandler(IAppDbContext db)
    : IRequestHandler<GetClinicDiaryQuery, IReadOnlyList<DiarySlotDto>>
{
    /// <summary>Unknown clinic → 404. Booked cells include PatientName + confirmation code.</summary>
    public async Task<IReadOnlyList<DiarySlotDto>> Handle(
        GetClinicDiaryQuery request,
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

        var slots = await db.TimeSlots
            .AsNoTracking()
            .Where(s =>
                s.ClinicId == request.ClinicId
                && s.StartUtc >= startUtc
                && s.StartUtc < endUtc)
            .OrderBy(s => s.StartUtc)
            .Select(s => new
            {
                s.Id,
                s.StartUtc,
                s.EndUtc,
                s.Status
            })
            .ToListAsync(cancellationToken);

        var slotIds = slots.Select(s => s.Id).ToList();
        var booked = await db.Appointments
            .AsNoTracking()
            .Where(a => slotIds.Contains(a.TimeSlotId) && a.Status == AppointmentStatus.Booked)
            .Select(a => new
            {
                a.TimeSlotId,
                a.Id,
                a.ConfirmationCode,
                PatientName = a.Patient.FirstName + " " + a.Patient.LastName
            })
            .ToListAsync(cancellationToken);

        var bySlot = booked.ToDictionary(a => a.TimeSlotId);
        return slots.Select(s =>
        {
            bySlot.TryGetValue(s.Id, out var row);
            return new DiarySlotDto(
                s.Id,
                s.StartUtc,
                s.EndUtc,
                s.Status,
                row?.PatientName,
                row?.ConfirmationCode,
                row?.Id);
        }).ToList();
    }

    /// <summary>Windows TZ locally; IANA fallback if this API is running in Linux Docker.</summary>
    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
        }
    }
}
