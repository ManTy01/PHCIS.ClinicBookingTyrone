using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClinicBooking.Blazor.Auth;
using ClinicBooking.Blazor.Models;

namespace ClinicBooking.Blazor.Api;

/// <summary>
/// PDF HttpClient: WASM talks to the API only. All booking rules stay on the server.
/// ApplyAuth copies the JWT onto the request (belt-and-braces with BearerHandler).
/// sealed: one client type.
/// </summary>
public sealed class ClinicApiClient(HttpClient http, AuthSession session)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Public — no JWT required.</summary>
    public Task<List<ClinicDto>> GetClinicsAsync() =>
        GetRequiredAsync<List<ClinicDto>>("api/clinics");

    /// <summary>Admin JWT. 201 Created. 403 Not authorised for staff/patient.</summary>
    public Task<ClinicDto> CreateClinicAsync(string name, string address, string? phone) =>
        PostAsync<ClinicDto>("api/clinics", new { name, address, phone }, HttpStatusCode.Created);

    /// <summary>Admin JWT. 200. 403 Not authorised for staff/patient.</summary>
    public Task<ClinicDto> UpdateClinicAsync(Guid clinicId, string name, string address, string? phone) =>
        PutAsync<ClinicDto>($"api/clinics/{clinicId}", new { name, address, phone });

    /// <summary>Public one-day schedule.</summary>
    public Task<List<TimeSlotDto>> GetSlotsAsync(Guid clinicId, DateOnly date) =>
        GetRequiredAsync<List<TimeSlotDto>>($"api/clinics/{clinicId}/slots?date={date:yyyy-MM-dd}");

    /// <summary>Public week grid (from/to inclusive).</summary>
    public Task<List<TimeSlotDto>> GetSlotsAsync(Guid clinicId, DateOnly from, DateOnly to) =>
        GetRequiredAsync<List<TimeSlotDto>>($"api/clinics/{clinicId}/slots?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    /// <summary>Staff JWT required — 403 for patients.</summary>
    public Task<List<DiarySlotDto>> GetDiaryAsync(Guid clinicId, DateOnly from, DateOnly to) =>
        GetRequiredAsync<List<DiarySlotDto>>($"api/staff/clinics/{clinicId}/diary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    /// <summary>Patient JWT required.</summary>
    public Task<List<AppointmentDto>> GetMyAppointmentsAsync() =>
        GetRequiredAsync<List<AppointmentDto>>("api/appointments");

    /// <summary>Returns JWT in AuthResult.Token — caller stores it in AuthSession.</summary>
    public Task<AuthResult> LoginAsync(string email, string password) =>
        PostAsync<AuthResult>("api/auth/login", new { email, password });

    /// <summary>Creates Patient + returns JWT so they can book immediately.</summary>
    public Task<AuthResult> RegisterAsync(string email, string password, string firstName, string lastName) =>
        PostAsync<AuthResult>("api/auth/register", new { email, password, firstName, lastName });

    /// <summary>Any authenticated role. GET /api/me.</summary>
    public Task<ProfileDto> GetMyProfileAsync() =>
        GetRequiredAsync<ProfileDto>("api/me");

    /// <summary>PUT /api/me — response includes a fresh JWT if email changed.</summary>
    public Task<AuthResult> UpdateMyProfileAsync(
        string email,
        string? firstName,
        string? lastName,
        string? phone,
        DateOnly? dateOfBirth) =>
        PutAsync<AuthResult>("api/me", new { email, firstName, lastName, phone, dateOfBirth });

    /// <summary>201 Created. 409 if the slot was taken (PDF: no double bookings).</summary>
    public Task<BookAppointmentResult> BookAsync(Guid timeSlotId) =>
        PostAsync<BookAppointmentResult>("api/appointments", new { timeSlotId }, HttpStatusCode.Created);

    /// <summary>204 No Content on success.</summary>
    public async Task CancelAsync(Guid appointmentId, string? reason)
    {
        ApplyAuth();
        var response = await http.PostAsJsonAsync($"api/appointments/{appointmentId}/cancel", new { reason }, Json);
        await EnsureSuccess(response);
    }

    /// <summary>Returns the new confirmation code.</summary>
    public Task<BookAppointmentResult> RescheduleAsync(Guid appointmentId, Guid newTimeSlotId) =>
        PostAsync<BookAppointmentResult>($"api/appointments/{appointmentId}/reschedule", new { newTimeSlotId });

    /// <summary>Staff JWT. 200 + message (that hour is now blocked).</summary>
    public Task<SlotActionResult> BlockSlotAsync(Guid slotId) =>
        PostAsync<SlotActionResult>($"api/staff/slots/{slotId}/block", new { });

    /// <summary>Staff JWT. 200 + message (that hour is available again).</summary>
    public Task<SlotActionResult> UnblockSlotAsync(Guid slotId) =>
        PostAsync<SlotActionResult>($"api/staff/slots/{slotId}/unblock", new { });

    /// <summary>PDF JWT: set or clear the Bearer header from AuthSession before each call.</summary>
    private void ApplyAuth()
    {
        if (session.IsLoggedIn)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        }
        else
        {
            http.DefaultRequestHeaders.Authorization = null;
        }
    }

    private async Task<T> GetRequiredAsync<T>(string url)
    {
        ApplyAuth();
        var response = await http.GetAsync(url);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>Book returns 201; login returns 200. Both are success.</summary>
    private async Task<T> PostAsync<T>(string url, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        ApplyAuth();
        var response = await http.PostAsJsonAsync(url, body, Json);
        if (response.StatusCode != expected && !response.IsSuccessStatusCode)
        {
            await EnsureSuccess(response);
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> PutAsync<T>(string url, object body)
    {
        ApplyAuth();
        var response = await http.PutAsJsonAsync(url, body, Json);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>Lift ProblemDetails.Detail into HttpRequestException so pages can show the API message.</summary>
    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? message = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(Json);
            message = problem?.Detail ?? problem?.Title;
        }
        catch (JsonException)
        {
            // 401 often has an empty body. 403 now sends ProblemDetails "Not authorised".
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException(message ?? "Not authorised", null, response.StatusCode);
        }

        throw new HttpRequestException(
            message ?? response.ReasonPhrase ?? "Request failed.",
            null,
            response.StatusCode);
    }
}
