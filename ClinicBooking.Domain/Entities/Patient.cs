namespace ClinicBooking.Domain.Entities;

/// <summary>
/// The person booking. Login lives in ASP.NET Identity (Infrastructure);
/// UserId is the bridge (JWT sub claim). Keep PHI light: name, email, phone, date of birth.
/// Not sealed: EF entity.
/// </summary>
public class Patient
{
    public Guid Id { get; set; }

    /// <summary>ASP.NET Identity user id (string GUID). Not a navigation — Identity is not in this project.</summary>
    public required string UserId { get; set; }

    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
