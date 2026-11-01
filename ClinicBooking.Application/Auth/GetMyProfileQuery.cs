using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Auth;

/// <summary>What /api/me returns. Staff/admin have IsPatient false and empty name fields.</summary>
public sealed record ProfileDto(
    string Email,
    IReadOnlyList<string> Roles,
    bool IsPatient,
    string? FirstName,
    string? LastName,
    string? Phone,
    DateOnly? DateOfBirth);

/// <summary>Read-only query. UserId comes from the JWT, never from the client body.</summary>
public sealed record GetMyProfileQuery(string UserId) : IRequest<ProfileDto>;

/// <summary>Loads Identity user (always) and Patient row (only Patient role). sealed: leaf handler.</summary>
public sealed class GetMyProfileQueryHandler(IUserDirectory users, IAppDbContext db)
    : IRequestHandler<GetMyProfileQuery, ProfileDto>
{
    /// <summary>404 if the JWT user id is not in Identity (should not happen for a valid token).</summary>
    public async Task<ProfileDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var patient = await db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        return new ProfileDto(
            user.Email,
            user.Roles,
            patient is not null,
            patient?.FirstName,
            patient?.LastName,
            patient?.Phone,
            patient?.DateOfBirth);
    }
}
