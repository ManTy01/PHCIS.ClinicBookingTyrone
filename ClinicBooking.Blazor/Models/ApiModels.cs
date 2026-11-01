namespace ClinicBooking.Blazor.Models;

/// <summary>Mirrors API ClinicDto. Blazor does not reference Domain/Application projects.</summary>
public sealed record ClinicDto(Guid Id, string Name, string Address, string? Phone);

/// <summary>Public slot cell. Status only — no patient name.</summary>
public sealed record TimeSlotDto(Guid Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, TimeSlotStatus Status);

/// <summary>Staff diary cell — includes PII the public board must not show.</summary>
public sealed record DiarySlotDto(
    Guid Id,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    TimeSlotStatus Status,
    string? PatientName,
    string? ConfirmationCode,
    Guid? AppointmentId);

/// <summary>My appointments row.</summary>
public sealed record AppointmentDto(
    Guid Id,
    Guid ClinicId,
    string ClinicName,
    Guid TimeSlotId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    AppointmentStatus Status,
    string ConfirmationCode);

/// <summary>PDF JWT: Token is the Bearer string Blazor stores in localStorage.</summary>
public sealed record AuthResult(string Token, string Email, IReadOnlyList<string> Roles);

/// <summary>/api/me payload.</summary>
public sealed record ProfileDto(
    string Email,
    IReadOnlyList<string> Roles,
    bool IsPatient,
    string? FirstName,
    string? LastName,
    string? Phone,
    DateOnly? DateOfBirth);

/// <summary>PDF confirmation message fields.</summary>
public sealed record BookAppointmentResult(Guid AppointmentId, string ConfirmationCode, DateTimeOffset StartUtc);

/// <summary>Staff block/unblock 200 body. Mirrors API SlotActionResult.</summary>
public sealed record SlotActionResult(string Message);

/// <summary>ASP.NET ProblemDetails subset for error text on the UI.</summary>
public sealed record ProblemDetailsDto(string? Title, string? Detail, int? Status);

/// <summary>
/// SignalR SlotChanged payload from SignalRSlotBoardNotifier.
/// Property names match the anonymous object (clinicId, timeSlotId, status).
/// Status is "Available" / "Booked" / "Blocked" — no patient name, so the hub stays anonymous.
/// sealed record: immutable DTO, not inherited.
/// </summary>
public sealed record SlotBoardChange(Guid ClinicId, Guid TimeSlotId, string Status);

/// <summary>Mirrors the API enums (JSON strings). Blazor does not reference Domain.</summary>
public enum AppointmentStatus
{
    Booked = 1,
    Cancelled = 2,
    Rescheduled = 3
}

public enum TimeSlotStatus
{
    Available = 1,
    Booked = 2,
    Blocked = 3
}
