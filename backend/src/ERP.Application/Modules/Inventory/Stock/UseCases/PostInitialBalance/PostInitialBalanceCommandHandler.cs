using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Inventory.Stock.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.PostInitialBalance;

/// <inheritdoc cref="PostInitialBalanceCommand"/>
/// <remarks>
/// Crea el <see cref="StockAdjustment"/> (Ingreso, fecha de documento = Fecha de Corte) y lo
/// ejecuta en el mismo SaveChanges. No abre transacción propia: dentro de la confirmación de Carga
/// Inicial participa de la transacción única del lote (StockRepository se suma a la transacción
/// ambiente), así cualquier fallo revierte documento, líneas, stock y Kardex.
/// </remarks>
public sealed class PostInitialBalanceCommandHandler
    : IRequestHandler<PostInitialBalanceCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly ICompanyClock _companyClock;
    private readonly ICompanyPrecisionPolicyProvider _precision;
    private readonly StockAdjustmentLineResolver _lineResolver;

    public PostInitialBalanceCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IStockRepository stockRepo,
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
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _stockRepo = stockRepo;
        _warehouseRepo = warehouseRepo;
        _itemRepo = itemRepo;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
        _companyClock = companyClock;
        _precision = precision;
        _lineResolver = new StockAdjustmentLineResolver(itemRepo, precision);
    }

    public async Task<Result<StockAdjustmentDto>> Handle(PostInitialBalanceCommand request, CancellationToken ct)
    {
        var tid = _tenant.TenantId;
        var companyId = _company.CompanyId;

        var warehouse = await _warehouseRepo.GetByIdAsync(tid, request.WarehouseId, ct);
        if (warehouse is null || !warehouse.IsActive)
            return Result<StockAdjustmentDto>.ValidationFailure("La bodega no existe o está inactiva.");
        if (warehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.ValidationFailure("La bodega no pertenece a la sucursal activa.");

        var reason = await _reasonRepo.GetByIdAsync(tid, request.ReasonId, ct);
        if (reason is null || !reason.IsActive || !reason.AllowsMovementType(StockAdjustment.MovementTypeIngreso))
            return Result<StockAdjustmentDto>.ValidationFailure(
                "El motivo de apertura no existe, está inactivo o no admite Ingreso.");

        var today = await _companyClock.TodayAsync(companyId, tid, ct);
        if (request.CutoffDate > today)
            return Result<StockAdjustmentDto>.ValidationFailure(
                $"La fecha de corte {request.CutoffDate:yyyy-MM-dd} es posterior al día operativo de la empresa.");

        foreach (var input in request.Lines)
        {
            var item = await _itemRepo.GetByIdLightAsync(input.ItemId, tid, ct);
            if (item is null || item.CompanyId != companyId || !item.IsActive || !item.ParticipatesInInventory)
                return Result<StockAdjustmentDto>.ValidationFailure(
                    $"El ítem '{input.ItemName}' no existe, está inactivo o no participa de inventario.");
            if (item.StockConfig.TracksLot || item.StockConfig.TracksSeries)
                return Result<StockAdjustmentDto>.ValidationFailure(
                    $"El ítem '{input.ItemName}' controla lotes o series; la apertura base no los admite.");
            if (input.UnitCostBase is not > 0)
                return Result<StockAdjustmentDto>.ValidationFailure(
                    $"El ítem '{input.ItemName}' requiere un costo unitario mayor a cero.");
            if (await _stockRepo.GetStockAsync(tid, request.WarehouseId, input.ItemId, ct) is not null)
                return Result<StockAdjustmentDto>.ValidationFailure(
                    $"El ítem '{input.ItemName}' ya tiene stock en la bodega; la apertura solo aplica sin historia.");
        }

        var lines = await _lineResolver.ResolveAsync(tid, companyId, request.Lines, ct);
        if (!lines.IsSuccess)
            return Result<StockAdjustmentDto>.ValidationFailure(lines.Error!);

        var adj = StockAdjustment.Create(
            tid,
            await _adjRepo.GetNextSequentialAsync(tid, ct),
            request.WarehouseId,
            request.WarehouseName,
            StockAdjustment.MovementTypeIngreso,
            request.ReasonId,
            request.Notes,
            _user.UserId,
            companyId,
            request.CutoffDate
        );
        adj.ReplaceLines(lines.Value!);
        await _adjRepo.AddAsync(adj, ct);

        var precision = await _precision.GetEffectiveAsync(ct);
        foreach (var line in adj.Lines)
        {
            var movement = await _stockRepo.AppendMovementAsync(
                tid,
                companyId,
                line.ItemId,
                request.WarehouseId,
                StockMovementType.InitialBalance,
                line.QuantityInBaseUom,
                line.BaseUomCode,
                request.CutoffDate,
                adj.AdjustmentNumber,
                adj.Id,
                "StockAdjustment",
                _user.UserId,
                unitCost: line.UnitCostBase,
                cancellationToken: ct,
                sourceDocLineId: line.Id
            );
            // Invariante de apertura: el saldo inicial es el primer movimiento de su Kardex. Si no lo
            // es, apareció historia (o el ítem se repite en el documento): se aborta todo.
            if (movement.SequenceNumber != 1)
                throw new DomainRuleViolationException(
                    $"El ítem '{line.ItemName}' ya tiene movimientos en la bodega; la apertura solo aplica sin historia.");

            line.ApplyExecutionResult(0m, movement.ResultQuantity, line.UnitCostBase, precision.UnitCostDecimals);
        }

        adj.Execute(_user.UserId);
        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason.Name, isInitialBalance: true));
    }
}
