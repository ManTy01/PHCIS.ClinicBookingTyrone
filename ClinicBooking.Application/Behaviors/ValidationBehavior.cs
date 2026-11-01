using FluentValidation;
using MediatR;

namespace ClinicBooking.Application.Behaviors;

/// <summary>
/// PDF FluentValidation: runs validators on every MediatR request before the handler.
/// Invalid commands never reach EF. Controllers stay thin.
/// sealed: pipeline behavior is a leaf type (generic, not inherited).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>DI injects every IValidator&lt;TRequest&gt; registered from the Application assembly.</summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>No validators → pass through. Failures throw ValidationException → HTTP 400.</summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
