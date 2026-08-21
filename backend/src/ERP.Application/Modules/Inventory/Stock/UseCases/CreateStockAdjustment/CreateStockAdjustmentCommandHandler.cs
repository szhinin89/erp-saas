using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-5) — el comando trae <c>WarehouseId</c> del cliente sin ninguna
/// validación de pertenencia: no existía lookup de bodega ni comparación contra la sucursal
/// activa, permitiendo crear un ajuste de inventario contra una bodega de otra sucursal (incluso
/// inexistente) de la misma empresa. <c>IWarehouseRepository.GetByIdAsync</c> ya scopea por
/// Company (<c>ForOperationalScope</c>), así que solo falta el chequeo de Branch — mismo patrón
/// que <c>OpenCashSessionHandler</c>/<c>CreateStockTransfer</c>.
/// </summary>
public sealed class CreateStockAdjustmentCommandHandler
    : IRequestHandler<CreateStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public CreateStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _adjRepo = adjRepo;
        _warehouseRepo = warehouseRepo;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        CreateStockAdjustmentCommand request,
        CancellationToken ct
    )
    {
        var warehouse = await _warehouseRepo.GetByIdAsync(_tenant.TenantId, request.WarehouseId, ct);
        if (warehouse is null)
            return Result<StockAdjustmentDto>.ValidationFailure("La bodega seleccionada no existe.");
        if (warehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.ValidationFailure(
                "La bodega seleccionada no pertenece a la sucursal activa."
            );

        var seq = await _adjRepo.GetNextSequentialAsync(_tenant.TenantId, ct);

        var adj = StockAdjustment.Create(
            _tenant.TenantId,
            seq,
            request.WarehouseId,
            request.WarehouseName,
            request.ProductId,
            request.ProductName,
            request.AdjustmentQty,
            request.Reason,
            request.Notes,
            _user.UserId,
            _company.CompanyId
        );

        await _adjRepo.AddAsync(adj, ct);
        await _adjRepo.SaveChangesAsync(ct);

        return Result<StockAdjustmentDto>.Success(ToDto(adj));
    }

    internal static StockAdjustmentDto ToDto(StockAdjustment a) =>
        new(
            a.Id,
            a.AdjustmentNumber,
            a.WarehouseId,
            a.WarehouseName,
            a.ProductId,
            a.ProductName,
            a.AdjustmentQty,
            a.AdjustmentType,
            a.Reason,
            a.Notes,
            a.AdjustmentDate,
            a.Status,
            a.ExecutedAt
        );
}
