using ClinicBooking.Application.Abstractions;
using FluentValidation;
using MediatR;

namespace ClinicBooking.Application.Auth;

/// <summary>
/// CQRS command: "log this person in". A record is an immutable request DTO.
/// sealed: MediatR matches the exact type — subclasses would not hit this handler.
/// </summary>
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

/// <summary>
/// FluentValidation (PDF) runs in ValidationBehavior before the handler.
/// sealed: one validator per command type.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

/// <summary>
/// Checks Identity via <see cref="IUserDirectory"/>, then mints a JWT (PDF: JWT Tokens).
/// The controller never sees passwords after this — it just returns the token string.
/// sealed: handlers are leaf types; we do not inherit from them.
/// </summary>
public sealed class LoginCommandHandler(IUserDirectory users, IJwtTokenService jwt)
    : IRequestHandler<LoginCommand, AuthResult>
{
    /// <summary>Returns 401 (UnauthorizedAccessException) when email/password do not match.</summary>
    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.AuthenticateAsync(request.Email.Trim(), request.Password, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        // JWT carries user id, email, and roles so [Authorize(Roles = "Patient")] works later.
        var token = jwt.CreateToken(user.UserId, user.Email, user.Roles);
        return new AuthResult(token, user.Email, user.Roles);
    }
}
