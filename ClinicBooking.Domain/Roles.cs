namespace ClinicBooking.Domain;

/// <summary>
/// ASP.NET Identity role names. Domain owns the names so API [Authorize(Roles)] and tests
/// cannot drift ("Staff" vs "ClinicStaff"). These strings go into the JWT role claim.
/// </summary>
public static class Roles
{
    public const string Patient = "Patient";
    public const string ClinicStaff = "ClinicStaff";
    public const string Admin = "Admin";
}
