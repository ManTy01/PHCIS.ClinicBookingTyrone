using ClinicBooking.Application.Abstractions;
using ClinicBooking.Infrastructure.Auth;
using ClinicBooking.Infrastructure.Identity;
using ClinicBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBooking.Infrastructure;

/// <summary>Extension methods so Api Program.cs stays one line: AddInfrastructure(cs).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// SQL Server + Identity (PDF). AddIdentityCore — not AddIdentity — so we do not
    /// install cookie auth that would fight JWT bearer.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserDirectory, UserDirectory>();
        // Singleton: token minting is stateless (reads JwtOptions). PDF JWT.
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
