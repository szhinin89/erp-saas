using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.EjecutarAjuste;

public sealed class ExecuteStockAdjustmentCommandHandler
    : IRequestHandler<ExecuteStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _ajusteRepo;
    private readonly IStockRepository  _inventario;
    private readonly ICostoPromedioService       _costoServicio;
    private readonly IUserActivityRepository     _activity;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ICurrentSubscriber              _currentSubscriber;
    private readonly ICurrentUser                _currentUser;
    private readonly ILogger<ExecuteStockAdjustmentCommandHandler> _logger;

    public ExecuteStockAdjustmentCommandHandler(
        IStockAdjustmentRepository ajusteRepo,
        IStockRepository inventario,
        ICostoPromedioService costoServicio,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<ExecuteStockAdjustmentCommandHandler> logger)
    {
        _ajusteRepo    = ajusteRepo;
        _inventario    = inventario;
        _costoServicio = costoServicio;
        _activity      = activity;
        _unitOfWork    = unitOfWork;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        ExecuteStockAdjustmentCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var ajuste = await _ajusteRepo.GetByIdAsync(subscriberId, command.AdjustmentId, ct);
        if (ajuste is null)
            return Result<StockAdjustmentDto>.Failure("Ajuste no encontrado.");

        if (ajuste.Status != "Draft")
            return Result<StockAdjustmentDto>.Failure(
                $"Solo se puede ejecutar un ajuste en Draft (estado actual: {ajuste.Status}).");

        _logger.LogInformation(
            "Ejecutando ajuste {Numero} ({Id}): {Cantidad} en Warehouse {Warehouse}",
            ajuste.AdjustmentNumber, ajuste.Id, ajuste.AdjustmentQty, ajuste.WarehouseName);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            decimal cantidadAnterior;
            StockMovementType tipoMovimiento;
            decimal? costoUnitarioMovimiento;

            if (ajuste.AdjustmentQty > 0)
            {
                // Incremento: UPSERT atómico — siempre tiene éxito.
                // Los ajustes de entrada no tienen costo de compra conocido.
                cantidadAnterior = await _inventario.IncrementStockAtomicAsync(
                    subscriberId, ajuste.WarehouseId, ajuste.ProductId,
                    ajuste.AdjustmentQty, userId, ct, unitCost: 0m);
                tipoMovimiento       = StockMovementType.PositiveAdjust;
                costoUnitarioMovimiento = null; // sin valorización para entrada manual
            }
            else
            {
                // Disminución: obtener costo promedio actual ANTES de decrementar.
                var averageCost = await _costoServicio.ObtenerCostoPromedioAsync(
                    subscriberId, ajuste.ProductId, ajuste.WarehouseId, ct);

                var cantAnteriorNullable = await _inventario.DecrementStockAtomicAsync(
                    subscriberId, ajuste.WarehouseId, ajuste.ProductId,
                    Math.Abs(ajuste.AdjustmentQty), userId, ct, averageCost);

                if (cantAnteriorNullable is null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    _logger.LogWarning(
                        "Stock insuficiente al ejecutar ajuste {Numero}: solicitado={S}",
                        ajuste.AdjustmentNumber, ajuste.AdjustmentQty);
                    return Result<StockAdjustmentDto>.Failure(
                        $"Stock insuficiente en '{ajuste.WarehouseName}' para disminuir " +
                        $"{Math.Abs(ajuste.AdjustmentQty)} unidades de '{ajuste.ProductName}'.");
                }

                cantidadAnterior        = cantAnteriorNullable.Value;
                tipoMovimiento          = StockMovementType.NegativeAdjust;
                costoUnitarioMovimiento = averageCost;
            }

            await _inventario.AddMovementAsync(
                StockMovement.Create(
                    subscriberId, ajuste.ProductId, ajuste.WarehouseId,
                    tipoMovimiento,
                    quantity:            ajuste.AdjustmentQty,
                    previousQuantity:    cantidadAnterior,
                    reference:          ajuste.AdjustmentNumber,
                    sourceDocId:   ajuste.Id,
                    sourceDocType: "StockAdjustment",
                    createdBy: userId,
                    unitCost:       costoUnitarioMovimiento,
                    companyId:      ajuste.CompanyId),
                ct);

            ajuste.Execute(userId);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "ajuste.ejecutar",
                entityType: "StockAdjustment", entityId: ajuste.Id,
                description: ajuste.AdjustmentNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Ajuste ejecutado: {Numero} ({Id})", ajuste.AdjustmentNumber, ajuste.Id);

            return Result<StockAdjustmentDto>.Success(ToDto(ajuste));
        }
        catch (InvalidOperationException ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogWarning(ex, "Error de negocio al ejecutar ajuste {Id}", command.AdjustmentId);
            return Result<StockAdjustmentDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error inesperado al ejecutar ajuste {Id}", command.AdjustmentId);
            return Result<StockAdjustmentDto>.Failure($"Error al ejecutar el ajuste: {ex.Message}");
        }
    }

    private static StockAdjustmentDto ToDto(StockAdjustment a) => new(
        a.Id, a.AdjustmentNumber,
        a.WarehouseId,   a.WarehouseName,
        a.ProductId, a.ProductName,
        a.AdjustmentQty, a.AdjustmentType,
        a.Reason, a.Notes,
        a.AdjustmentDate, a.Status,
        a.ExecutedAt, a.ExecutedBy,
        a.CreatedAt);
}
