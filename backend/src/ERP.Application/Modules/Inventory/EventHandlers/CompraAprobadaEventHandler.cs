using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class PurchBillApprovedEventHandler : INotificationHandler<PurchBillApprovedEvent>
{
    private readonly IStockRepository _inventario;
    private readonly ILogger<PurchBillApprovedEventHandler> _logger;

    public PurchBillApprovedEventHandler(
        IStockRepository inventario,
        ILogger<PurchBillApprovedEventHandler> logger)
    {
        _inventario = inventario;
        _logger     = logger;
    }

    public async Task Handle(PurchBillApprovedEvent notification, CancellationToken ct)
    {
        var subscriberId = notification.SubscriberId;
        var userId   = notification.ApprovedByUserId;

        foreach (var line in notification.StockLines)
        {
            if (!line.ProductId.HasValue)
            {
                _logger.LogWarning(
                    "Compra {CompraId}: línea sin producto; no se actualiza inventario (detalle {DetalleId}, Warehouse {BodegaId}).",
                    notification.PurchBillId, line.BillLineId, line.WarehouseId);
                continue;
            }

            var productoId = line.ProductId.Value;

            var stock = await _inventario.GetStockAsync(
                subscriberId, line.WarehouseId, productoId, ct);
            if (stock is null)
            {
                stock = CurrentStock.Create(subscriberId, productoId, line.WarehouseId, userId);
                await _inventario.AddCurrentStockAsync(stock, ct);
            }

            var cantidadAnterior = stock.Quantity;
            stock.ApplyMovement(line.Quantity, userId, line.NetUnitCost);

            var movimiento = StockMovement.Create(
                subscriberId,
                productoId,
                line.WarehouseId,
                StockMovementType.PurchaseEntry,
                quantity:            line.Quantity,
                previousQuantity:    cantidadAnterior,
                reference:          notification.InvoiceNumber,
                sourceDocId:   notification.PurchBillId,
                sourceDocType: "PurchBill",
                createdBy: userId,
                unitCost:       line.NetUnitCost);

            await _inventario.AddMovementAsync(movimiento, ct);
        }
    }
}
