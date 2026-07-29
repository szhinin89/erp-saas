using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEstablishments;

public sealed class GetEstablishmentsQueryHandler
    : IRequestHandler<GetEstablishmentsQuery, Result<IReadOnlyList<EstablishmentListItemDto>>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;

    public GetEstablishmentsQueryHandler(
        IEstablishmentRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany company
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _company = company;
    }

    public async Task<Result<IReadOnlyList<EstablishmentListItemDto>>> Handle(
        GetEstablishmentsQuery request,
        CancellationToken cancellationToken
    )
    {
        var items = await _repo.GetFilteredAsync(
            _currentTenant.TenantId,
            _company.CompanyId,
            request.BranchId,
            request.IsActive,
            request.Search,
            cancellationToken
        );

        var dtos = items.Select(ToListItemDto).ToList();
        return Result<IReadOnlyList<EstablishmentListItemDto>>.Success(dtos);
    }

    internal static EstablishmentListItemDto ToListItemDto(Establishment e) =>
        new(
            e.Id,
            e.Code,
            e.Name,
            e.Address,
            e.Phone,
            e.BranchId,
            e.Branch?.Name,
            e.EmissionPoints.Count(ep => ep.IsActive),
            e.IsMain,
            e.IsActive,
            e.CreatedAt
        );
}
