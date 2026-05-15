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
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Inventory.UseCases.ConfirmarTransferencia;

public sealed class ConfirmarTransferenciaCommandHandler
    : IRequestHandler<ConfirmarTransferenciaCommand, Result<TransferenciaDto>>
{
    private readonly IStockTransferRepository   _transferenciaRepo;
    private readonly IStockRepository _inventario;
    private readonly ICostoPromedioService      _costoServicio;
    private readonly IProductRepository         _productRepo;
    private readonly IUserActivityRepository    _activity;
    private readonly IUnitOfWork                _unitOfWork;
    private readonly ICurrentTenant             _currentTenant;
    private readonly ICurrentUser               _currentUser;
    private readonly ILogger<ConfirmarTransferenciaCommandHandler> _logger;

    public ConfirmarTransferenciaCommandHandler(
        IStockTransferRepository transferenciaRepo,
        IStockRepository inventario,
        ICostoPromedioService costoServicio,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<ConfirmarTransferenciaCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _inventario        = inventario;
        _costoServicio     = costoServicio;
        _productRepo       = productRepo;
        _activity          = activity;
        _unitOfWork        = unitOfWork;
        _currentTenant     = currentTenant;
        _currentUser       = currentUser;
        _logger            = logger;
    }

    public async Task<Result<TransferenciaDto>> Handle(
        ConfirmarTransferenciaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var transfer = await _transferenciaRepo.GetByIdAsync(tenantId, command.TransferenciaId, ct);
        if (transfer is null)
            return Result<TransferenciaDto>.Failure("transfer no encontrada.");

        if (transfer.Status != "Borrador")
            return Result<TransferenciaDto>.Failure(
                $"Solo se puede confirmar una transfer en Borrador (estado actual: {transfer.Status}).");

        if (transfer.Lines.Count == 0)
            return Result<TransferenciaDto>.Failure("La transfer no tiene ítems.");

        _logger.LogInformation(
            "Confirmando transfer {Numero} ({Id}): {Count} ítems",
            transfer.TransferNumber, transfer.Id, transfer.Lines.Count);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var detalle in transfer.Lines)
            {
                var producto = await _productRepo.GetByIdAsync(detalle.ProductId, tenantId, ct);
                if (producto is not null && (producto.IsService || !producto.TracksStock))
                    continue;

                // Obtener el costo promedio actual de la Warehouse ORIGEN antes del decremento.
                // Se usa para valorizar la salida y la entrada en destino con el mismo costo.
                var costoPromedio = await _costoServicio.ObtenerCostoPromedioAsync(
                    tenantId, detalle.ProductId, transfer.SourceWarehouseId, ct);

                // ── Decremento atómico en Warehouse ORIGEN ───────────────────────
                var cantAnteriorOrigen = await _inventario.DecrementStockAtomicAsync(
                    tenantId, transfer.SourceWarehouseId, detalle.ProductId,
                    detalle.Quantity, userId, ct, costoPromedio);

                if (cantAnteriorOrigen is null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    _logger.LogWarning(
                        "Stock insuficiente (posiblemente por concurrencia): producto={Pid}, solicitado={S}",
                        detalle.ProductId, detalle.Quantity);
                    return Result<TransferenciaDto>.Failure(
                        $"Stock insuficiente para '{detalle.Description}' en la Warehouse origen. " +
                        $"El saldo pudo haber sido modificado por otra operación concurrente.");
                }

                await _inventario.AddMovementAsync(
                    StockMovement.Create(
                        tenantId, detalle.ProductId, transfer.SourceWarehouseId,
                        StockMovementType.TransferExit,
                        quantity:            -detalle.Quantity,
                        previousQuantity:    cantAnteriorOrigen.Value,
                        reference:          transfer.TransferNumber,
                        sourceDocId:   transfer.Id,
                        sourceDocType: "transfer",
                        createdBy: userId,
                        unitCost:       costoPromedio),
                    ct);

                // ── Incremento atómico en Warehouse DESTINO ──────────────────────
                var cantAnteriorDestino = await _inventario.IncrementStockAtomicAsync(
                    tenantId, transfer.TargetWarehouseId, detalle.ProductId,
                    detalle.Quantity, userId, ct, costoPromedio);

                await _inventario.AddMovementAsync(
                    StockMovement.Create(
                        tenantId, detalle.ProductId, transfer.TargetWarehouseId,
                        StockMovementType.TransferEntry,
                        quantity:            +detalle.Quantity,
                        previousQuantity:    cantAnteriorDestino,
                        reference:          transfer.TransferNumber,
                        sourceDocId:   transfer.Id,
                        sourceDocType: "transfer",
                        createdBy: userId,
                        unitCost:       costoPromedio),
                    ct);
            }

            transfer.Confirm(userId);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "transfer.confirmar",
                entityType: "transfer", entityId: transfer.Id,
                description: transfer.TransferNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "transfer confirmada: {Numero} ({Id})",
                transfer.TransferNumber, transfer.Id);

            return Result<TransferenciaDto>.Success(new TransferenciaDto(
                transfer.Id, transfer.TransferNumber,
                transfer.SourceWarehouseId,
                transfer.SourceWarehouse?.Name ?? transfer.SourceWarehouseId.ToString(),
                transfer.TargetWarehouseId,
                transfer.TargetWarehouse?.Name ?? transfer.TargetWarehouseId.ToString(),
                transfer.TransferDate, transfer.Status,
                transfer.Reason, transfer.Notes,
                transfer.ConfirmedAt, transfer.ConfirmedBy,
                transfer.CreatedAt));
        }
        catch (InvalidOperationException ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogWarning(ex, "Error de negocio al confirmar transfer {Id}", command.TransferenciaId);
            return Result<TransferenciaDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error inesperado al confirmar transfer {Id}", command.TransferenciaId);
            return Result<TransferenciaDto>.Failure($"Error al confirmar la transfer: {ex.Message}");
        }
    }
}
