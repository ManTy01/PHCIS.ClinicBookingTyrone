using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Clinics;

/// <summary>
/// Admin-only create. TimeZoneId is not on the body — PHCIS clinics are all SAST.
/// sealed: MediatR exact type.
/// </summary>
public sealed record CreateClinicCommand(string Name, string Address, string? Phone) : IRequest<ClinicDto>;

/// <summary>PDF FluentValidation — name and address required before we touch SQL.</summary>
public sealed class CreateClinicCommandValidator : AbstractValidator<CreateClinicCommand>
{
    public CreateClinicCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(32);
    }
}

/// <summary>
/// Inserts the clinic and the same weekday hour grid as DatabaseSeeder.
/// Unique clinic name is checked first; the unique index is the race backup → 409.
/// sealed: leaf handler.
/// </summary>
public sealed class CreateClinicCommandHandler(IAppDbContext db)
    : IRequestHandler<CreateClinicCommand, ClinicDto>
{
    /// <summary>Returns the public card DTO. Duplicate name throws ConflictException.</summary>
    public async Task<ClinicDto> Handle(CreateClinicCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var address = request.Address.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        var nameTaken = await db.Clinics.AnyAsync(c => c.Name == name, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException("A clinic with that name already exists.");
        }

        var clinic = new Clinic
        {
            Id = Guid.NewGuid(),
            Name = name,
            Address = address,
            Phone = phone,
            TimeZoneId = ClinicHourSlots.DefaultTimeZoneId
        };

        db.Clinics.Add(clinic);
        foreach (var slot in ClinicHourSlots.BuildWeekdayHours(clinic.Id, clinic.TimeZoneId))
        {
            db.TimeSlots.Add(slot);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("A clinic with that name already exists.");
        }

        return new ClinicDto(clinic.Id, clinic.Name, clinic.Address, clinic.Phone);
    }

    /// <summary>SQL unique-index failure (two admins, same name). Message text varies by provider.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_Clinics_Name", StringComparison.OrdinalIgnoreCase);
    }
}
