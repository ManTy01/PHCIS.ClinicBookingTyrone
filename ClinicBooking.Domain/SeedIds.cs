namespace ClinicBooking.Domain;

/// <summary>Stable GUIDs so seed and tests can find the two demo clinics.</summary>
public static class SeedIds
{
    public static readonly Guid JhbClinic = Guid.Parse("8f1e3c2a-7b64-4d91-9c0e-1a2b3c4d5e6f");
    public static readonly Guid CptClinic = Guid.Parse("a4c8d912-3e70-4b55-8f21-9d6e5c4b3a20");
}
