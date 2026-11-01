namespace ClinicBooking.Application.Common;

/// <summary>
/// Maps to HTTP 409 — email already registered, or the slot was taken (PDF: no double bookings).
/// sealed: leaf exception type.
/// </summary>
public sealed class ConflictException : Exception
{
    /// <summary>Message becomes ProblemDetails.Detail on the 409 response.</summary>
    public ConflictException(string message)
        : base(message)
    {
    }
}
