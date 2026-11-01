using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain;
using Microsoft.AspNetCore.Identity;

namespace ClinicBooking.Infrastructure.Identity;

/// <summary>
/// PDF: ASP.NET Identity. This is the only class that talks to UserManager.
/// Application handlers call IUserDirectory so they never reference Identity packages.
/// sealed: one implementation.
/// </summary>
public sealed class UserDirectory(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles)
    : IUserDirectory
{
    /// <summary>Create user + role. Duplicate email → 409. Password rules come from Identity options (min 8).</summary>
    public async Task<RegisteredUser> CreateUserAsync(
        string email,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with that email already exists.");
        }

        if (!await roles.RoleExistsAsync(role))
        {
            await roles.CreateAsync(new IdentityRole(role));
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            var detail = string.Join(" ", created.Errors.Select(e => e.Description));
            throw new ConflictException(detail);
        }

        await users.AddToRoleAsync(user, role);
        return new RegisteredUser(user.Id, user.Email!);
    }

    /// <summary>Login check. Null on unknown email or bad password — caller maps that to 401.</summary>
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            return null;
        }

        var userRoles = await users.GetRolesAsync(user);
        return new AuthenticatedUser(user.Id, user.Email!, userRoles.ToList());
    }

    /// <summary>Used by /api/me — load by the JWT sub claim.</summary>
    public async Task<AuthenticatedUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var userRoles = await users.GetRolesAsync(user);
        return new AuthenticatedUser(user.Id, user.Email!, userRoles.ToList());
    }

    /// <summary>Keep Email and UserName in sync (we log in with email). No-op if unchanged.</summary>
    public async Task UpdateEmailAsync(string userId, string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        var trimmed = email.Trim();
        if (string.Equals(user.Email, trimmed, StringComparison.OrdinalIgnoreCase)
            && string.Equals(user.UserName, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var existing = await users.FindByEmailAsync(trimmed);
        if (existing is not null && existing.Id != userId)
        {
            throw new ConflictException("An account with that email already exists.");
        }

        var setEmail = await users.SetEmailAsync(user, trimmed);
        if (!setEmail.Succeeded)
        {
            throw new ConflictException(string.Join(" ", setEmail.Errors.Select(e => e.Description)));
        }

        var setName = await users.SetUserNameAsync(user, trimmed);
        if (!setName.Succeeded)
        {
            throw new ConflictException(string.Join(" ", setName.Errors.Select(e => e.Description)));
        }
    }
}
