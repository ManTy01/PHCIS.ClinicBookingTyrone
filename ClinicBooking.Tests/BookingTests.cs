using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClinicBooking.Application.Appointments;
using ClinicBooking.Application.Auth;
using ClinicBooking.Application.Clinics;
using ClinicBooking.Application.Slots;

namespace ClinicBooking.Tests;

/// <summary>
/// End-to-end against LocalDB via WebApplicationFactory.
/// Login tests prove JWT: no token → 401; Bearer token → book; second book → 409.
/// </summary>
public class BookingTests : IClassFixture<ClinicBookingWebAppFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public BookingTests(ClinicBookingWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>Seed must expose Jhb and Cpt with no JWT.</summary>
    [Fact]
    public async Task GetClinics_ReturnsSeededClinics()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);

        Assert.NotNull(clinics);
        Assert.Contains(clinics, c => c.Name.Contains("Jhb"));
        Assert.Contains(clinics, c => c.Name.Contains("Cpt"));
    }

    /// <summary>Public board shows Booked status but JSON must not contain the patient name/email.</summary>
    [Fact]
    public async Task GetSlots_Anonymous_ShowsBookedWithoutPatientData()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var clinicId = clinics[0].Id;

        var slot = await FirstAvailableSlotAsync(clinicId);
        Assert.NotNull(slot);

        await LoginAsPatientAsync();
        var booked = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = slot!.Id });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;

        var date = DateOnly.FromDateTime(slot.StartUtc.ToOffset(TimeSpan.FromHours(2)).DateTime);
        var response = await _client.GetAsync($"/api/clinics/{clinicId}/slots?date={date:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var slots = JsonSerializer.Deserialize<List<TimeSlotDto>>(json, Json);

        Assert.NotNull(slots);
        var publicSlot = Assert.Single(slots!, s => s.Id == slot.Id);
        Assert.Equal(Domain.Enums.TimeSlotStatus.Booked, publicSlot.Status);
        Assert.DoesNotContain("John", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Doe", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient@", json, StringComparison.OrdinalIgnoreCase);

        await LoginAsPatientAsync();
        var created = await booked.Content.ReadFromJsonAsync<BookAppointmentResult>(Json);
        Assert.NotNull(created);
        var cancel = await _client.PostAsJsonAsync($"/api/appointments/{created.AppointmentId}/cancel", new { reason = "public board cleanup" });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    /// <summary>PDF JWT: posting a booking with no Authorization header must be 401.</summary>
    [Fact]
    public async Task Book_WithoutJwt_IsUnauthorized()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var slot = await FirstAvailableSlotAsync(clinics[0].Id);
        Assert.NotNull(slot);

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = slot!.Id });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>PDF no double bookings: same JWT, same slot, second call is 409.</summary>
    [Fact]
    public async Task BookTwice_SameSlot_SecondIsConflict()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var clinicId = clinics[0].Id;

        var slot = await FirstAvailableSlotAsync(clinicId);
        Assert.NotNull(slot);

        await LoginAsPatientAsync();

        var first = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = slot!.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = slot.Id });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var booked = await first.Content.ReadFromJsonAsync<BookAppointmentResult>(Json);
        Assert.NotNull(booked);
        Assert.StartsWith("PHC-", booked.ConfirmationCode);

        var cancel = await _client.PostAsJsonAsync($"/api/appointments/{booked.AppointmentId}/cancel", new { reason = "test cleanup" });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    [Fact]
    public async Task GetSlots_HourWindows()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var slot = await FirstAvailableSlotAsync(clinics[0].Id);
        Assert.NotNull(slot);
        Assert.Equal(TimeSpan.FromHours(1), slot!.EndUtc - slot.StartUtc);
    }

    [Fact]
    public async Task GetMyAppointments_AfterBook_ReturnsRow()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var slot = await FirstAvailableSlotAsync(clinics[0].Id);
        Assert.NotNull(slot);

        await LoginAsPatientAsync();
        var booked = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = slot!.Id });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var created = await booked.Content.ReadFromJsonAsync<BookAppointmentResult>(Json);
        Assert.NotNull(created);

        var mine = await _client.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", Json);
        Assert.NotNull(mine);
        Assert.Contains(mine, a => a.Id == created.AppointmentId && a.Status == Domain.Enums.AppointmentStatus.Booked);

        var cancel = await _client.PostAsJsonAsync($"/api/appointments/{created.AppointmentId}/cancel", new { reason = "list cleanup" });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    [Fact]
    public async Task StaffDiary_ShowsPatientName_PublicSlotsDoNot()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var clinicId = clinics[0].Id;
        var slot = await FirstAvailableSlotAsync(clinicId);
        Assert.NotNull(slot);

        await LoginAsPatientAsync();
        var booked = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = slot!.Id });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var created = await booked.Content.ReadFromJsonAsync<BookAppointmentResult>(Json);
        Assert.NotNull(created);

        var date = DateOnly.FromDateTime(slot.StartUtc.ToOffset(TimeSpan.FromHours(2)).DateTime);

        await LoginAsStaffAsync();
        var diary = await _client.GetFromJsonAsync<List<DiarySlotDto>>(
            $"/api/staff/clinics/{clinicId}/diary?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}",
            Json);
        Assert.NotNull(diary);
        var diarySlot = Assert.Single(diary!, s => s.Id == slot.Id);
        Assert.Equal(Domain.Enums.TimeSlotStatus.Booked, diarySlot.Status);
        Assert.Contains("John", diarySlot.PatientName, StringComparison.OrdinalIgnoreCase);

        await LoginAsPatientAsync();
        var asPatient = await _client.GetAsync($"/api/staff/clinics/{clinicId}/diary?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");
        await AssertNotAuthorisedAsync(asPatient);

        var cancel = await _client.PostAsJsonAsync($"/api/appointments/{created.AppointmentId}/cancel", new { reason = "diary cleanup" });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    [Fact]
    public async Task StaffBlock_ThenPublicBoardShowsBlocked_AndBookIsConflict()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var clinicId = clinics[0].Id;
        var slot = await FirstAvailableSlotAsync(clinicId);
        Assert.NotNull(slot);

        await LoginAsStaffAsync();
        var blocked = await _client.PostAsync($"/api/staff/slots/{slot!.Id}/block", null);
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        var blockedBody = await blocked.Content.ReadFromJsonAsync<SlotActionResult>(Json);
        Assert.Contains("blocked", blockedBody!.Message, StringComparison.OrdinalIgnoreCase);

        _client.DefaultRequestHeaders.Authorization = null;
        var date = DateOnly.FromDateTime(slot.StartUtc.ToOffset(TimeSpan.FromHours(2)).DateTime);
        var publicSlots = await _client.GetFromJsonAsync<List<TimeSlotDto>>(
            $"/api/clinics/{clinicId}/slots?date={date:yyyy-MM-dd}",
            Json);
        Assert.Equal(Domain.Enums.TimeSlotStatus.Blocked, publicSlots!.Single(s => s.Id == slot.Id).Status);

        await LoginAsPatientAsync();
        var book = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = slot.Id });
        Assert.Equal(HttpStatusCode.Conflict, book.StatusCode);

        await LoginAsStaffAsync();
        var unblocked = await _client.PostAsync($"/api/staff/slots/{slot.Id}/unblock", null);
        Assert.Equal(HttpStatusCode.OK, unblocked.StatusCode);
        var unblockedBody = await unblocked.Content.ReadFromJsonAsync<SlotActionResult>(Json);
        Assert.Contains("available", unblockedBody!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reschedule_MarksOldRowAndBooksNewSlot()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var clinicId = clinics[0].Id;

        var firstSlot = await FirstAvailableSlotAsync(clinicId);
        Assert.NotNull(firstSlot);

        await LoginAsPatientAsync();

        var bookedResponse = await _client.PostAsJsonAsync("/api/appointments", new { timeSlotId = firstSlot!.Id });
        Assert.Equal(HttpStatusCode.Created, bookedResponse.StatusCode);
        var booked = await bookedResponse.Content.ReadFromJsonAsync<BookAppointmentResult>(Json);
        Assert.NotNull(booked);

        var nextSlot = await FirstAvailableSlotAsync(clinicId);
        Assert.NotNull(nextSlot);
        Assert.NotEqual(firstSlot.Id, nextSlot!.Id);

        var reschedule = await _client.PostAsJsonAsync(
            $"/api/appointments/{booked.AppointmentId}/reschedule",
            new { newTimeSlotId = nextSlot.Id });
        Assert.Equal(HttpStatusCode.OK, reschedule.StatusCode);

        var mine = await _client.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", Json);
        Assert.NotNull(mine);
        Assert.Contains(mine, a => a.Id == booked.AppointmentId && a.Status == Domain.Enums.AppointmentStatus.Rescheduled);
        Assert.Contains(mine, a => a.TimeSlotId == nextSlot.Id && a.Status == Domain.Enums.AppointmentStatus.Booked);
    }

    /// <summary>PDF JWT: /api/me is [Authorize] with no anonymous bypass.</summary>
    [Fact]
    public async Task GetMe_WithoutJwt_IsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_Patient_ReturnsOwnDetails()
    {
        await LoginAsPatientAsync();
        var profile = await _client.GetFromJsonAsync<ProfileDto>("/api/me", Json);
        Assert.NotNull(profile);
        Assert.True(profile.IsPatient);
        Assert.Equal("patient@phcis.local", profile.Email);
        Assert.Equal("John", profile.FirstName);
        Assert.Equal("Doe", profile.LastName);
        Assert.Contains("Patient", profile.Roles);
    }

    [Fact]
    public async Task PutMe_Patient_UpdatesPhoneAndDateOfBirth()
    {
        await LoginAsPatientAsync();
        var updated = await _client.PutAsJsonAsync("/api/me", new
        {
            email = "patient@phcis.local",
            firstName = "John",
            lastName = "Doe",
            phone = "0820000000",
            dateOfBirth = "1990-05-01"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var profile = await _client.GetFromJsonAsync<ProfileDto>("/api/me", Json);
        Assert.NotNull(profile);
        Assert.Equal("0820000000", profile.Phone);
        Assert.Equal(new DateOnly(1990, 5, 1), profile.DateOfBirth);
    }

    [Fact]
    public async Task GetMe_Staff_HasNoPatientDetails()
    {
        await LoginAsStaffAsync();
        var profile = await _client.GetFromJsonAsync<ProfileDto>("/api/me", Json);
        Assert.NotNull(profile);
        Assert.False(profile.IsPatient);
        Assert.Equal("staff@phcis.local", profile.Email);
        Assert.Null(profile.FirstName);
        Assert.Contains("ClinicStaff", profile.Roles);
    }

    /// <summary>Admin JWT. Creates a clinic and weekday hour slots so GET /slots is not empty.</summary>
    [Fact]
    public async Task CreateClinic_AsAdmin_AppearsInListWithHourSlots()
    {
        await LoginAsAdminAsync();
        var name = $"Durban Clinic {Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/api/clinics", new
        {
            name,
            address = "18th Avenue, Durban",
            phone = "011 000 1003"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var clinic = await created.Content.ReadFromJsonAsync<ClinicDto>(Json);
        Assert.NotNull(clinic);
        Assert.Equal(name, clinic.Name);

        _client.DefaultRequestHeaders.Authorization = null;
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.Contains(clinics!, c => c.Id == clinic.Id && c.Phone == "011 000 1003");

        var slot = await FirstAvailableSlotAsync(clinic.Id);
        Assert.NotNull(slot);
        Assert.Equal(TimeSpan.FromHours(1), slot!.EndUtc - slot.StartUtc);
    }

    /// <summary>Admin JWT. Name/address/phone change; slots stay as they were.</summary>
    [Fact]
    public async Task UpdateClinic_AsAdmin_ChangesCard()
    {
        await LoginAsAdminAsync();
        var name = $"Kimberly Clinic {Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/api/clinics", new
        {
            name,
            address = "Kimberly Civic Centre",
            phone = "011 000 1004"
        });
        var clinic = await created.Content.ReadFromJsonAsync<ClinicDto>(Json);
        Assert.NotNull(clinic);

        var updatedName = $"{name} West";
        var updated = await _client.PutAsJsonAsync($"/api/clinics/{clinic.Id}", new
        {
            name = updatedName,
            address = "Kimberly Hospital Road",
            phone = "011 000 1999"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        var row = Assert.Single(clinics!, c => c.Id == clinic.Id);
        Assert.Equal(updatedName, row.Name);
        Assert.Equal("Kimberly Hospital Road", row.Address);
        Assert.Equal("011 000 1999", row.Phone);
    }

    /// <summary>Staff JWT is authenticated but not Admin — 403 with "Not authorised".</summary>
    [Fact]
    public async Task CreateClinic_AsStaff_IsNotAuthorised()
    {
        await LoginAsStaffAsync();
        var response = await _client.PostAsJsonAsync("/api/clinics", new
        {
            name = "Should Fail Clinic",
            address = "Nowhere",
            phone = (string?)null
        });
        await AssertNotAuthorisedAsync(response);
    }

    /// <summary>Patient JWT cannot edit clinics either.</summary>
    [Fact]
    public async Task UpdateClinic_AsPatient_IsNotAuthorised()
    {
        var clinics = await _client.GetFromJsonAsync<List<ClinicDto>>("/api/clinics", Json);
        Assert.NotNull(clinics);
        var clinicId = clinics[0].Id;

        await LoginAsPatientAsync();
        var response = await _client.PutAsJsonAsync($"/api/clinics/{clinicId}", new
        {
            name = "Hacked name",
            address = "Hacked",
            phone = (string?)null
        });
        await AssertNotAuthorisedAsync(response);
    }

    /// <summary>No JWT on POST /api/clinics is 401 (not signed in), not 403.</summary>
    [Fact]
    public async Task CreateClinic_WithoutJwt_IsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/clinics", new
        {
            name = "Anonymous Clinic",
            address = "Nowhere",
            phone = (string?)null
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task LoginAsPatientAsync() => LoginAsync("patient@phcis.local");

    private Task LoginAsStaffAsync() => LoginAsync("staff@phcis.local");

    private Task LoginAsAdminAsync() => LoginAsync("admin@phcis.local");

    /// <summary>Wrong role on [Authorize] now returns ProblemDetails Detail = Not authorised.</summary>
    private static async Task AssertNotAuthorisedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Not authorised", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>POST /api/auth/login then put the JWT on DefaultRequestHeaders.Authorization.</summary>
    private async Task LoginAsync(string email)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResult>(Json);
        Assert.NotNull(auth);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
    }

    /// <summary>Walk upcoming days until an Available future hour is found (public GET, no JWT).</summary>
    private async Task<TimeSlotDto?> FirstAvailableSlotAsync(Guid clinicId)
    {
        for (var i = 0; i < 14; i++)
        {
            var date = DateOnly.FromDateTime(DateTime.Today.AddDays(i));
            var slots = await _client.GetFromJsonAsync<List<TimeSlotDto>>(
                $"/api/clinics/{clinicId}/slots?date={date:yyyy-MM-dd}",
                Json);
            var open = slots?.FirstOrDefault(s =>
                s.Status == Domain.Enums.TimeSlotStatus.Available
                && s.StartUtc > DateTimeOffset.UtcNow);
            if (open is not null)
            {
                return open;
            }
        }

        return null;
    }
}
