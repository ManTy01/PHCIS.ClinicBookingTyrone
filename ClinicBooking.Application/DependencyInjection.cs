using ClinicBooking.Application.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBooking.Application;

/// <summary>Extension methods so Api Program.cs stays one line: AddApplication().</summary>
public static class DependencyInjection
{
    /// <summary>
    /// MediatR (CQRS), FluentValidation, AutoMapper (all on the PDF stack).
    /// Call from Api Program.cs — not from Infrastructure.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(AssemblyMarker).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        return services;
    }
}
