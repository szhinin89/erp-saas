using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class SalesBillAuthorizedEventHandler : INotificationHandler<SalesBillAuthorizedEvent>
{
    private readonly IStockRepository _inventario;
    private readonly ILogger<SalesBillAuthorizedEventHandler> _logger;

    public SalesBillAuthorizedEventHandler(
        IStockRepository inventario,
        ILogger<SalesBillAuthorizedEventHandler> logger)
    {
        _inventario = inventario;
        _logger     = logger;
    }

    public async Task Handle(SalesBillAuthorizedEvent notification, CancellationToken ct)
    {
        var subscriberId = notification.SubscriberId;
        var userId   = notification.UserId;
        var bodegaId = notification.WarehouseId;

        foreach (var line in notification.StockLines)
        {
            var stock = await _inventario.GetStockAsync(
                subscriberId, bodegaId, line.ProductId, ct);

            if (stock is null)
            {
                _logger.LogWarning(
                    "Venta {FacturaId}: sin stock registrado para producto {ProductoId} en Warehouse {BodegaId}; se omite descuento.",
                    notification.SalesBillId, line.ProductId, bodegaId);
                continue;
            }

            var cantidadAnterior = stock.Quantity;
            var costoPromedioVenta = stock.AverageCost;
            stock.ApplyMovement(-line.Quantity, userId, costoPromedioVenta);

            var movimiento = StockMovement.Create(
                subscriberId,
                line.ProductId,
                bodegaId,
                StockMovementType.SaleExit,
                quantity:            -line.Quantity,
                previousQuantity:    cantidadAnterior,
                reference:          notification.BillNumber,
                sourceDocId:   notification.SalesBillId,
                sourceDocType: "SalesBill",
                createdBy: userId,
                unitCost:       costoPromedioVenta);

            await _inventario.AddMovementAsync(movimiento, ct);
        }
    }
}
