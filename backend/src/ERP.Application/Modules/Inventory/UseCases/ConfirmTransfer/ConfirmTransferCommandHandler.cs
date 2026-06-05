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

namespace ERP.Application.Inventory.UseCases.ConfirmTransfer;

public sealed class ConfirmTransferCommandHandler
    : IRequestHandler<ConfirmTransferCommand, Result<TransferDto>>
{
    private readonly IStockTransferRepository   _transferenciaRepo;
    private readonly IStockRepository _inventario;
    private readonly IAverageCostService      _costoServicio;
    private readonly IProductRepository         _productRepo;
    private readonly IUserActivityRepository    _activity;
    private readonly IUnitOfWork                _unitOfWork;
    private readonly ICurrentSubscriber             _currentSubscriber;
    private readonly ICurrentUser               _currentUser;
    private readonly ILogger<ConfirmTransferCommandHandler> _logger;

    public ConfirmTransferCommandHandler(
        IStockTransferRepository transferenciaRepo,
        IStockRepository inventario,
        IAverageCostService costoServicio,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<ConfirmTransferCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _inventario        = inventario;
        _costoServicio     = costoServicio;
        _productRepo       = productRepo;
        _activity          = activity;
        _unitOfWork        = unitOfWork;
        _currentSubscriber     = currentSubscriber;
        _currentUser       = currentUser;
        _logger            = logger;
    }

    public async Task<Result<TransferDto>> Handle(
        ConfirmTransferCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var transfer = await _transferenciaRepo.GetByIdAsync(subscriberId, command.TransferId, ct);
        if (transfer is null)
            return Result<TransferDto>.Failure("transfer no encontrada.");

        if (transfer.Status != "Draft")
            return Result<TransferDto>.Failure(
                $"Solo se puede confirmar una transfer en Draft (estado actual: {transfer.Status}).");

        if (transfer.Lines.Count == 0)
            return Result<TransferDto>.Failure("La transfer no tiene ítems.");

        _logger.LogInformation(
            "Confirmando transfer {Numero} ({Id}): {Count} ítems",
            transfer.TransferNumber, transfer.Id, transfer.Lines.Count);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var line in transfer.Lines)
            {
                var producto = await _productRepo.GetByIdAsync(line.ProductId, subscriberId, ct);
                if (producto is not null && (producto.IsService || !producto.TracksStock))
                    continue;

                // Obtener el costo promedio actual de la Warehouse ORIGEN antes del decremento.
                // Se usa para valorizar la salida y la entrada en destino con el mismo costo.
                var averageCost = await _costoServicio.ObtenerCostoPromedioAsync(
                    subscriberId, line.ProductId, transfer.SourceWarehouseId, ct);

                // ── Decremento atómico en Warehouse ORIGEN ───────────────────────
                var cantAnteriorOrigen = await _inventario.DecrementStockAtomicAsync(
                    subscriberId, transfer.SourceWarehouseId, line.ProductId,
                    line.Quantity, userId, ct, averageCost);

                if (cantAnteriorOrigen is null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    _logger.LogWarning(
                        "Stock insuficiente (posiblemente por concurrencia): producto={Pid}, solicitado={S}",
                        line.ProductId, line.Quantity);
                    return Result<TransferDto>.Failure(
                        $"Stock insuficiente para '{line.Description}' en la Warehouse origen. " +
                        $"El saldo pudo haber sido modificado por otra operación concurrente.");
                }

                await _inventario.AddMovementAsync(
                    StockMovement.Create(
                        subscriberId, line.ProductId, transfer.SourceWarehouseId,
                        StockMovementType.TransferExit,
                        quantity:            -line.Quantity,
                        previousQuantity:    cantAnteriorOrigen.Value,
                        reference:          transfer.TransferNumber,
                        sourceDocId:   transfer.Id,
                        sourceDocType: "transfer",
                        createdBy: userId,
                        unitCost:       averageCost,
                        companyId:      transfer.CompanyId),
                    ct);

                // ── Incremento atómico en Warehouse DESTINO ──────────────────────
                var cantAnteriorDestino = await _inventario.IncrementStockAtomicAsync(
                    subscriberId, transfer.TargetWarehouseId, line.ProductId,
                    line.Quantity, userId, ct, averageCost);

                await _inventario.AddMovementAsync(
                    StockMovement.Create(
                        subscriberId, line.ProductId, transfer.TargetWarehouseId,
                        StockMovementType.TransferEntry,
                        quantity:            +line.Quantity,
                        previousQuantity:    cantAnteriorDestino,
                        reference:          transfer.TransferNumber,
                        sourceDocId:   transfer.Id,
                        sourceDocType: "transfer",
                        createdBy: userId,
                        unitCost:       averageCost,
                        companyId:      transfer.CompanyId),
                    ct);
            }

            transfer.Confirm(userId);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "transfer.confirmar",
                entityType: "transfer", entityId: transfer.Id,
                description: transfer.TransferNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "transfer confirmada: {Numero} ({Id})",
                transfer.TransferNumber, transfer.Id);

            return Result<TransferDto>.Success(new TransferDto(
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
            _logger.LogWarning(ex, "Error de negocio al confirmar transfer {Id}", command.TransferId);
            return Result<TransferDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error inesperado al confirmar transfer {Id}", command.TransferId);
            return Result<TransferDto>.Failure($"Error al confirmar la transfer: {ex.Message}");
        }
    }
}
