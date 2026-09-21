using ERP.Application.Modules.Companies;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Inventory.Stock.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-5) — el comando trae <c>WarehouseId</c> del cliente sin ninguna
/// validación de pertenencia: no existía lookup de bodega ni comparación contra la sucursal
/// activa, permitiendo crear un ajuste de inventario contra una bodega de otra sucursal (incluso
/// inexistente) de la misma empresa. <c>IWarehouseRepository.GetByIdAsync</c> ya scopea por
/// Company (<c>ForOperationalScope</c>), así que solo falta el chequeo de Branch — mismo patrón
/// que <c>OpenCashSessionHandler</c>/<c>CreateStockTransfer</c>.
/// INVENTORY-ADJUSTMENTS-02 — no crea ningún StockMovement: solo persiste el borrador (Draft) con
/// sus líneas resueltas a unidad base (item + presentación → UomCode/ConversionFactor/
/// QuantityInBaseUom, mismo patrón que <c>PurchaseInvoiceDetail.Create</c>).
/// </summary>
public sealed class CreateStockAdjustmentCommandHandler
    : IRequestHandler<CreateStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly ICompanyClock _companyClock;
    private readonly StockAdjustmentLineResolver _lineResolver;
    private readonly ICompanyPrecisionPolicyProvider _precision;

    public CreateStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IWarehouseRepository warehouseRepo,
        IItemRepository itemRepo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user,
        ICompanyClock companyClock,
        ICompanyPrecisionPolicyProvider precision
    )
    {
        _precision = precision;
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _warehouseRepo = warehouseRepo;
        _itemRepo = itemRepo;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
        _companyClock = companyClock;
        _lineResolver = new StockAdjustmentLineResolver(itemRepo, precision);
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        CreateStockAdjustmentCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;

        var warehouse = await _warehouseRepo.GetByIdAsync(tid, request.WarehouseId, ct);
        if (warehouse is null)
            return Result<StockAdjustmentDto>.ValidationFailure("La bodega seleccionada no existe.");
        if (warehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.ValidationFailure(
                "La bodega seleccionada no pertenece a la sucursal activa."
            );

        var reason = await _reasonRepo.GetByIdAsync(tid, request.ReasonId, ct);
        if (reason is null)
            return Result<StockAdjustmentDto>.ValidationFailure("El motivo seleccionado no existe.");

        var lineResult = await _lineResolver.ResolveAsync(
            tid,
            _company.CompanyId,
            request.Lines,
            ct
        );
        if (!lineResult.IsSuccess)
            return Result<StockAdjustmentDto>.ValidationFailure(lineResult.Error!);

        var seq = await _adjRepo.GetNextSequentialAsync(tid, ct);
        var adjustmentDate = await _companyClock.TodayAsync(_company.CompanyId, tid, ct);

        var adj = StockAdjustment.Create(
            tid,
            seq,
            request.WarehouseId,
            request.WarehouseName,
            request.MovementType,
            request.ReasonId,
            request.Notes,
            _user.UserId,
            _company.CompanyId,
            adjustmentDate
        );
        adj.ReplaceLines(lineResult.Value!);

        await _adjRepo.AddAsync(adj, ct);
        await _adjRepo.SaveChangesAsync(ct);

        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason.Name));
    }
}
