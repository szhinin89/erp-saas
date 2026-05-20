using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class SalesNoteAuthorizedEventHandler : INotificationHandler<SalesNoteAuthorizedEvent>
{
    private readonly IStockRepository _inventario;
    private readonly ILogger<SalesNoteAuthorizedEventHandler> _logger;

    public SalesNoteAuthorizedEventHandler(
        IStockRepository inventario,
        ILogger<SalesNoteAuthorizedEventHandler> logger)
    {
        _inventario = inventario;
        _logger     = logger;
    }

    public async Task Handle(SalesNoteAuthorizedEvent notification, CancellationToken ct)
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
                    "Nota crédito {NotaId}: sin stock para producto {ProductoId} en Warehouse {BodegaId}; se omite reingreso.",
                    notification.NoteId, line.ProductId, bodegaId);
                continue;
            }

            var cantidadAnterior = stock.Quantity;
            var costo            = stock.AverageCost;
            stock.ApplyMovement(line.Quantity, userId, costo);

            var movimiento = StockMovement.Create(
                subscriberId,
                line.ProductId,
                bodegaId,
                StockMovementType.SaleReturn,
                quantity:            line.Quantity,
                previousQuantity:    cantidadAnterior,
                reference:          notification.NoteNumber,
                sourceDocId:   notification.NoteId,
                sourceDocType: "SalesNote",
                createdBy: userId,
                unitCost:       costo);

            await _inventario.AddMovementAsync(movimiento, ct);
        }
    }
}
