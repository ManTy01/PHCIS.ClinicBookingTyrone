namespace ClinicBooking.Application.Common;

/// <summary>Maps to HTTP 404 in ApiExceptionHandler. sealed: we do not subclass domain exceptions.</summary>
public sealed class NotFoundException : Exception
{
    /// <summary>Message becomes ProblemDetails.Detail.</summary>
    public NotFoundException(string message)
        : base(message)
    {
    }
}
