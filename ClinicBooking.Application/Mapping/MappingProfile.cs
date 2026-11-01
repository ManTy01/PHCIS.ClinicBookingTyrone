using AutoMapper;
using ClinicBooking.Application.Clinics;
using ClinicBooking.Application.Slots;
using ClinicBooking.Domain.Entities;

namespace ClinicBooking.Application.Mapping;

/// <summary>
/// PDF AutoMapper: entity → public DTO. Keep mapping here so handlers stay queries, not property lists.
/// sealed: AutoMapper Profile is configured once; we do not inherit from this profile.
/// </summary>
public sealed class MappingProfile : Profile
{
    /// <summary>Property names match (Id, Name, …) so CreateMap needs no ForMember.</summary>
    public MappingProfile()
    {
        CreateMap<Clinic, ClinicDto>();
        CreateMap<TimeSlot, TimeSlotDto>();
    }
}
