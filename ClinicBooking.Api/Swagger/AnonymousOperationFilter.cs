using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClinicBooking.Api.Swagger;

/// <summary>
/// Global Bearer security makes Swagger send an Authorization header on every Try it out.
/// Login/clinics/slots are anonymous — an empty Bearer there becomes 401 and looks "broken".
/// This filter clears security on [AllowAnonymous] (and on actions with no [Authorize]).
/// sealed: Swashbuckle filter, not inherited.
/// </summary>
public sealed class AnonymousOperationFilter : IOperationFilter
{
    /// <summary>SwaggerGen calls this once per operation while building the OpenAPI document.</summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var allowAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        var authorize = metadata.OfType<IAuthorizeData>().Any();
        if (allowAnonymous || !authorize)
        {
            operation.Security = [];
        }
    }
}
