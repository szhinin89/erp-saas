using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.ListarBodegas;

public sealed class GetBodegasQueryHandler
    : IRequestHandler<GetBodegasQuery, Result<IReadOnlyList<BodegaDto>>>
{
    private readonly IWarehouseRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetBodegasQueryHandler(IWarehouseRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<BodegaDto>>> Handle(
        GetBodegasQuery query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId, query.ActiveFilter, query.Search, query.BranchId, ct);

        var dtos = list.Select(b => new BodegaDto(
            b.Id, b.BranchId, b.Name, b.Address, b.Manager, b.IsActive))
            .ToList();

        return Result<IReadOnlyList<BodegaDto>>.Success(dtos);
    }
}
