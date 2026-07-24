using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Company.UseCases.GetEmissionPoints;

public sealed class GetAllEmissionPointsQueryHandler
    : IRequestHandler<GetAllEmissionPointsQuery, Result<IReadOnlyList<EmissionPointListItemDto>>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly ICurrentTenant           _currentTenant;
    private readonly ICurrentCompany          _company;

    public GetAllEmissionPointsQueryHandler(
        IEmissionPointRepository repo,
        ICurrentTenant           currentTenant,
        ICurrentCompany          company)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
        _company       = company;
    }

    public async Task<Result<IReadOnlyList<EmissionPointListItemDto>>> Handle(
        GetAllEmissionPointsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetAllByCompanyAsync(
            _currentTenant.TenantId,
            _company.CompanyId,
            request.ActiveFilter,
            request.Search,
            cancellationToken);

        var dtos = items.Select(ToDto).ToList();
        return Result<IReadOnlyList<EmissionPointListItemDto>>.Success(dtos);
    }

    private static EmissionPointListItemDto ToDto(EmissionPoint ep) => new(
        ep.Id,
        ep.EstablishmentId,
        ep.Establishment.Code,
        ep.Establishment.Name,
        ep.Establishment.Branch?.Name,
        ep.Code,
        ep.Name,
        ep.EmissionType,
        ep.IsDefault,
        ep.IsActive,
        ep.CreatedAt);
}
