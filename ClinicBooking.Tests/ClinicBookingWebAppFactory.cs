using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace ClinicBooking.Tests;

/// <summary>
/// PDF: WebApplicationFactory-style API tests against the real Program (LocalDB).
/// Development environment so migrate + seed run, same as F5.
/// sealed: one factory per test class fixture.
/// </summary>
public sealed class ClinicBookingWebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>Force Development so JWT, Swagger wiring, and DatabaseSeeder match local Visual Studio.</summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        return base.CreateHost(builder);
    }
}
