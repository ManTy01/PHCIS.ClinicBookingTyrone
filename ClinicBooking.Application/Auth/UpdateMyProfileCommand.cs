using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Auth;

/// <summary>Update own details. UserId is from the JWT so you cannot edit someone else.</summary>
public sealed record UpdateMyProfileCommand(
    string UserId,
    string Email,
    string? FirstName,
    string? LastName,
    string? Phone,
    DateOnly? DateOfBirth) : IRequest<AuthResult>;

/// <summary>PDF FluentValidation — email required; names required only when a Patient row exists (checked in handler).</summary>
public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).MaximumLength(64);
        RuleFor(x => x.LastName).MaximumLength(64);
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.DateOfBirth)
            .Must(d => d is null || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Date of birth cannot be in the future.");
    }
}

/// <summary>
/// Updates Identity email and, for patients, the Patients row.
/// Returns a new JWT because the email claim inside the old token would be stale.
/// sealed: leaf handler.
/// </summary>
public sealed class UpdateMyProfileCommandHandler(
    IUserDirectory users,
    IAppDbContext db,
    IJwtTokenService jwt) : IRequestHandler<UpdateMyProfileCommand, AuthResult>
{
    /// <summary>Staff/admin skip the Patient row and still get a fresh JWT with the new email.</summary>
    public async Task<AuthResult> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var email = request.Email.Trim();
        await users.UpdateEmailAsync(request.UserId, email, cancellationToken);

        var patient = await db.Patients
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (patient is not null)
        {
            var firstName = request.FirstName?.Trim() ?? string.Empty;
            var lastName = request.LastName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                throw new ConflictException("First name and last name are required.");
            }

            var emailTaken = await db.Patients.AnyAsync(
                p => p.Email == email && p.UserId != request.UserId,
                cancellationToken);
            if (emailTaken)
            {
                throw new ConflictException("An account with that email already exists.");
            }

            patient.Email = email;
            patient.FirstName = firstName;
            patient.LastName = lastName;
            patient.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            patient.DateOfBirth = request.DateOfBirth;
            await db.SaveChangesAsync(cancellationToken);
        }

        var token = jwt.CreateToken(user.UserId, email, user.Roles);
        return new AuthResult(token, email, user.Roles);
    }
}
