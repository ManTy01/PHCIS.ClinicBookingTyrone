using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClinicBooking.Infrastructure.Auth;

/// <summary>
/// PDF: JWT Tokens. Mints a signed token after login/register/profile update.
/// We write both short claim names (sub, email, role) AND ClaimTypes because
/// some JWT middleware maps inbound claims and some does not — [Authorize(Roles)]
/// and CurrentUserId() then both work.
/// sealed: one implementation of IJwtTokenService.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    /// <summary>HMAC-SHA256 token. Expiry comes from Jwt:ExpiryMinutes in appsettings.</summary>
    public string CreateToken(string userId, string email, IEnumerable<string> roles)
    {
        var jwt = options.Value;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Email, email)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.ExpiryMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
