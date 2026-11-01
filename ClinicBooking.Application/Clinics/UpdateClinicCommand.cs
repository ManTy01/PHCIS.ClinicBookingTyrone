using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Clinics;

/// <summary>Admin-only edit of name/address/phone. Does not rebuild slots. sealed: MediatR exact type.</summary>
public sealed record UpdateClinicCommand(Guid Id, string Name, string Address, string? Phone) : IRequest<ClinicDto>;

/// <summary>PDF FluentValidation — same field rules as create.</summary>
public sealed class UpdateClinicCommandValidator : AbstractValidator<UpdateClinicCommand>
{
    public UpdateClinicCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(32);
    }
}

/// <summary>
/// Updates the clinic card. Unknown id → 404. Duplicate name → 409.
/// sealed: leaf handler.
/// </summary>
public sealed class UpdateClinicCommandHandler(IAppDbContext db)
    : IRequestHandler<UpdateClinicCommand, ClinicDto>
{
    /// <summary>Does not touch TimeSlots — hours stay as seeded or previously generated.</summary>
    public async Task<ClinicDto> Handle(UpdateClinicCommand request, CancellationToken cancellationToken)
    {
        var clinic = await db.Clinics.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Clinic not found.");

        var name = request.Name.Trim();
        var address = request.Address.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        var nameTaken = await db.Clinics.AnyAsync(c => c.Name == name && c.Id != request.Id, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException("A clinic with that name already exists.");
        }

        clinic.Name = name;
        clinic.Address = address;
        clinic.Phone = phone;

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

    /// <summary>SQL unique-index failure if two edits race to the same name.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_Clinics_Name", StringComparison.OrdinalIgnoreCase);
    }
}
