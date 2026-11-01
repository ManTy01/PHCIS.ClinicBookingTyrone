using ClinicBooking.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Middleware;

/// <summary>
/// Turns domain/validation exceptions into ProblemDetails so controllers stay thin.
/// sealed: one handler registered in Program.cs — not a base class.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    /// <summary>Called by the exception middleware. Returns true when we wrote the response.</summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Server error")
        };

        httpContext.Response.StatusCode = status;

        if (exception is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await httpContext.Response.WriteAsJsonAsync(
                new HttpValidationProblemDetails(errors)
                {
                    Title = title,
                    Status = status
                },
                cancellationToken);
            return true;
        }

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = title,
                Detail = exception.Message,
                Status = status
            },
            cancellationToken);

        return true;
    }
}
