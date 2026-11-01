using ClinicBooking.Application.Abstractions;
using ClinicBooking.Domain;
using ClinicBooking.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ClinicBooking.Application.Auth;

/// <summary>JWT + email + roles returned by login, register, and profile update.</summary>
public sealed record AuthResult(string Token, string Email, IReadOnlyList<string> Roles);

/// <summary>
/// Register a Patient. Creates Identity user AND a Patients row (staff/admin have no Patient row).
/// sealed: MediatR request type is exact-match.
/// </summary>
public sealed record RegisterPatientCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone) : IRequest<AuthResult>;

/// <summary>PDF FluentValidation — password min 8 matches Identity options.</summary>
public sealed class RegisterPatientCommandValidator : AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(64);
    }
}

/// <summary>
/// Creates the login (Identity) then the Patient profile, then mints a JWT so the UI can book immediately.
/// sealed: leaf handler.
/// </summary>
public sealed class RegisterPatientCommandHandler(
    IUserDirectory users,
    IAppDbContext db,
    IJwtTokenService jwt) : IRequestHandler<RegisterPatientCommand, AuthResult>
{
    /// <summary>Duplicate email → ConflictException (409). Success → JWT for the new Patient.</summary>
    public async Task<AuthResult> Handle(RegisterPatientCommand request, CancellationToken cancellationToken)
    {
        var created = await users.CreateUserAsync(
            request.Email.Trim(),
            request.Password,
            Roles.Patient,
            cancellationToken);

        db.Patients.Add(new Patient
        {
            Id = Guid.NewGuid(),
            UserId = created.UserId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = created.Email,
            Phone = request.Phone
        });

        await db.SaveChangesAsync(cancellationToken);

        var token = jwt.CreateToken(created.UserId, created.Email, [Roles.Patient]);
        return new AuthResult(token, created.Email, [Roles.Patient]);
    }
}
