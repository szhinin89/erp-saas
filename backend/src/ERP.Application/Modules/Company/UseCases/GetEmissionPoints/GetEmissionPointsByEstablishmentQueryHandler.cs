using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.GetEmissionPoints;

public sealed class GetEmissionPointsByEstablishmentQueryHandler
    : IRequestHandler<GetEmissionPointsByEstablishmentQuery, Result<IReadOnlyList<EmissionPointDto>>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentTenant       _currentTenant;

    public GetEmissionPointsByEstablishmentQueryHandler(IEmissionPointRepository repo, ICurrentTenant tenant)
    {
        _repo = repo; _currentTenant = tenant;
    }

    public async Task<Result<IReadOnlyList<EmissionPointDto>>> Handle(
        GetEmissionPointsByEstablishmentQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetByEstablishmentAsync(_currentTenant.TenantId, request.EstablishmentId, cancellationToken);
        var dtos = items.Select(ToDto).ToList();
        return Result<IReadOnlyList<EmissionPointDto>>.Success(dtos);
    }

    internal static EmissionPointDto ToDto(Domain.Modules.Company.Entities.EmissionPoint ep) => new(
        ep.Id, ep.EstablishmentId, ep.CompanyId, ep.Code, ep.Name,
        ep.EmissionType, ep.IsDefault, ep.IsActive, ep.CreatedAt, ep.UpdatedAt);
}
