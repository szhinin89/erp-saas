using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.ObtenerBodega;

public sealed class GetBodegaByIdQueryHandler
    : IRequestHandler<GetBodegaByIdQuery, Result<BodegaDetailDto?>>
{
    private readonly IWarehouseRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetBodegaByIdQueryHandler(IWarehouseRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<BodegaDetailDto?>> Handle(
        GetBodegaByIdQuery query, CancellationToken ct)
    {
        var b = await _repo.GetByIdAsync(_tenant.TenantId, query.Id, ct);
        if (b is null) return Result<BodegaDetailDto?>.Success(null);

        return Result<BodegaDetailDto?>.Success(new BodegaDetailDto(
            b.Id, b.BranchId, b.Name, b.Address, b.Manager,
            b.IsActive, b.CreatedAt, b.UpdatedAt, b.CreatedBy, b.UpdatedBy));
    }
}
