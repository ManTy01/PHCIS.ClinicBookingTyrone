namespace ClinicBooking.Application.Common;

/// <summary>Short code shown after a successful book (PDF: confirmation messages), e.g. PHC-A1B2C3.</summary>
public static class ConfirmationCodes
{
    /// <summary>Unique enough for a demo; SQL unique index is the real uniqueness guarantee.</summary>
    public static string NewCode() => $"PHC-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
