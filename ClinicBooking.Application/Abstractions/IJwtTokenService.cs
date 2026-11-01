namespace ClinicBooking.Application.Abstractions;

/// <summary>
/// PDF: JWT Tokens. Application asks for a token; Infrastructure (JwtTokenService) knows HMAC details.
/// Keeping this interface in Application means handlers never reference Microsoft.IdentityModel.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Build a signed JWT with sub, email, and role claims for the given Identity user.</summary>
    string CreateToken(string userId, string email, IEnumerable<string> roles);
}
