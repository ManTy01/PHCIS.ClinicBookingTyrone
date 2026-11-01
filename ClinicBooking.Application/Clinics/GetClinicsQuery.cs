using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClinicBooking.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Application.Clinics;

/// <summary>Public clinic card — enough to pick Jhb or Cpt.</summary>
public sealed record ClinicDto(Guid Id, string Name, string Address, string? Phone);

/// <summary>No parameters — seeded clinics plus any admin-added rows. sealed: MediatR exact type.</summary>
public sealed record GetClinicsQuery : IRequest<IReadOnlyList<ClinicDto>>;

/// <summary>PDF AutoMapper ProjectTo so we do not hand-map columns. sealed: leaf handler.</summary>
public sealed class GetClinicsQueryHandler(IAppDbContext db, IMapper mapper)
    : IRequestHandler<GetClinicsQuery, IReadOnlyList<ClinicDto>>
{
    /// <summary>Alphabetical. AsNoTracking because this is a read.</summary>
    public async Task<IReadOnlyList<ClinicDto>> Handle(GetClinicsQuery request, CancellationToken cancellationToken)
    {
        return await db.Clinics
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ProjectTo<ClinicDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
