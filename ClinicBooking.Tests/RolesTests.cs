using ClinicBooking.Domain;

namespace ClinicBooking.Tests;

/// <summary>Smoke test that role strings (JWT claims / [Authorize(Roles)]) do not drift.</summary>
public class RolesTests
{
    /// <summary>The Patient constant is what we put in the JWT and on AppointmentsController.</summary>
    [Fact]
    public void Patient_role_name_is_stable()
    {
        Assert.Equal("Patient", Roles.Patient);
    }
}
