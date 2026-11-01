namespace ClinicBooking.Application.Auth;

/// <summary>
/// Binds the "Jwt" section in appsettings.json (PDF: JWT Tokens).
/// Issuer/Audience/Key must match what Program.cs validates on incoming Bearer tokens.
/// sealed: options bag, not a base class.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name: "Jwt".</summary>
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC signing key. Dev-only in appsettings — do not use this value in a real clinic.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>How long Blazor can keep the token in localStorage before login is required again.</summary>
    public int ExpiryMinutes { get; set; } = 120;
}
