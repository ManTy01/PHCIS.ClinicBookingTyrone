namespace ClinicBooking.Application.Abstractions;

/// <summary>Result of CreateUserAsync — Identity user id (string GUID) plus email.</summary>
public sealed record RegisteredUser(string UserId, string Email);

/// <summary>Result of login / FindById — includes roles so we can put them in the JWT.</summary>
public sealed record AuthenticatedUser(string UserId, string Email, IReadOnlyList<string> Roles);

/// <summary>
/// Identity wrapper so Application does not take a dependency on UserManager.
/// PDF: ASP.NET Identity lives in Infrastructure; this is the door Application uses.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Create Identity user in a role. Throws ConflictException if the email exists.</summary>
    Task<RegisteredUser> CreateUserAsync(string email, string password, string role, CancellationToken cancellationToken);

    /// <summary>Check password. Returns null on failure so the handler can throw 401 without leaking which field was wrong.</summary>
    Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Load by Identity id (the JWT sub claim).</summary>
    Task<AuthenticatedUser?> FindByIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Change email and user name together (we use email as the login name).</summary>
    Task UpdateEmailAsync(string userId, string email, CancellationToken cancellationToken);
}
