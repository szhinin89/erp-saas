using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class PurchNoteApprovedEventHandler : INotificationHandler<PurchNoteApprovedEvent>
{
    private readonly IStockRepository _inventario;
    private readonly ICurrentCompany _company;
    private readonly ILogger<PurchNoteApprovedEventHandler> _logger;

    public PurchNoteApprovedEventHandler(
        IStockRepository inventario,
        ICurrentCompany company,
        ILogger<PurchNoteApprovedEventHandler> logger)
    {
        _inventario = inventario;
        _company     = company;
        _logger      = logger;
    }

    public async Task Handle(PurchNoteApprovedEvent notification, CancellationToken ct)
    {
        var subscriberId = notification.SubscriberId;
        var userId   = notification.UserId;
        var companyId = _company.HasCompanyContext ? _company.CompanyId : (Guid?)null;

        var tipoMov = NoteTypeHelper.IsCredit(notification.NoteType)
            ? StockMovementType.SupplierCreditNote
            : StockMovementType.SupplierDebitNote;

        foreach (var line in notification.StockLines)
        {
            var stock = await _inventario.GetStockAsync(
                subscriberId, line.WarehouseId, line.ProductId, ct);
            if (stock is null)
            {
                if (line.QuantityDelta > 0)
                {
                    stock = CurrentStock.Create(subscriberId, line.ProductId, line.WarehouseId, userId, companyId: companyId);
                    await _inventario.AddCurrentStockAsync(stock, ct);
                }
                else
                {
                    _logger.LogWarning(
                        "Nota Supplier {NotaId}: no hay stock en Warehouse {BodegaId} producto {ProductoId} para aplicar salida.",
                        notification.NoteId, line.WarehouseId, line.ProductId);
                    continue;
                }
            }

            var cantidadAnterior = stock.Quantity;
            var costo            = line.UnitCost;
            if (line.QuantityDelta < 0 && stock.Quantity > 0)
                costo = stock.AverageCost;

            stock.ApplyMovement(line.QuantityDelta, userId, costo);

            var movimiento = StockMovement.Create(
                subscriberId,
                line.ProductId,
                line.WarehouseId,
                tipoMov,
                quantity:            line.QuantityDelta,
                previousQuantity:    cantidadAnterior,
                reference:          notification.NoteNumber,
                sourceDocId:   notification.NoteId,
                sourceDocType: "PurchNote",
                createdBy: userId,
                unitCost:       costo,
                companyId:      companyId);

            await _inventario.AddMovementAsync(movimiento, ct);
        }
    }
}
