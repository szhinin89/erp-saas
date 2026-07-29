using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEstablishments;

public sealed class GetEstablishmentsByBranchQueryHandler
    : IRequestHandler<GetEstablishmentsByBranchQuery, Result<IReadOnlyList<EstablishmentDto>>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetEstablishmentsByBranchQueryHandler(IEstablishmentRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _currentTenant = tenant;
    }

    public async Task<Result<IReadOnlyList<EstablishmentDto>>> Handle(
        GetEstablishmentsByBranchQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetByBranchAsync(_currentTenant.TenantId, request.BranchId, cancellationToken);
        var dtos = items.Select(ToDto).ToList();
        return Result<IReadOnlyList<EstablishmentDto>>.Success(dtos);
    }

    internal static EstablishmentDto ToDto(Domain.Modules.Company.Entities.Establishment e) => new(
        e.Id, e.BranchId, e.CompanyId, e.Code, e.Name, e.Address, e.Phone,
        e.IsMain, e.IsActive, e.CreatedAt, e.UpdatedAt);

}
