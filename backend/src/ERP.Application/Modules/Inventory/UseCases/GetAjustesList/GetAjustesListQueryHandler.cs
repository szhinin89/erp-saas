using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetAjustesList;

public sealed class GetAjustesListQueryHandler
    : IRequestHandler<GetAjustesListQuery, Result<AjustesPagedResult>>
{
    private readonly IStockAdjustmentRepository _repo;
    private readonly ICurrentTenant              _currentTenant;

    public GetAjustesListQueryHandler(
        IStockAdjustmentRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AjustesPagedResult>> Handle(
        GetAjustesListQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var (items, total) = await _repo.GetPagedAsync(
            tenantId, query.PageNumber, query.PageSize,
            query.WarehouseId, query.ProductId, query.Status,
            query.DateFrom, query.DateTo, ct);

        var dtos = items.Select(a => new StockAdjustmentDto(
            a.Id, a.AdjustmentNumber,
            a.WarehouseId, a.WarehouseName,
            a.ProductId, a.ProductName,
            a.AdjustmentQty, a.AdjustmentType,
            a.Reason, a.Notes,
            a.AdjustmentDate, a.Status,
            a.ExecutedAt, a.ExecutedBy,
            a.CreatedAt)).ToList();

        return Result<AjustesPagedResult>.Success(
            new AjustesPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}
