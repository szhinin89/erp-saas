using ERP.Application.Common.Inventory;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class SalesBillAuthorizedEventHandler : INotificationHandler<SalesBillAuthorizedEvent>
{
    private readonly IInventoryPostingService _posting;

    public SalesBillAuthorizedEventHandler(IInventoryPostingService posting) => _posting = posting;

    public async Task Handle(SalesBillAuthorizedEvent notification, CancellationToken ct)
    {
        if (notification.CompanyId == Guid.Empty || notification.StockLines.Count == 0)
            return;

        var lines = notification.StockLines
            .Select(l => new InventoryPostingLine(l.ProductId, l.Quantity))
            .ToList();

        var result = await _posting.PostSaleExitAsync(
            new InventoryPostingRequest(
                notification.SubscriberId,
                notification.CompanyId,
                notification.WarehouseId,
                lines,
                notification.BillNumber,
                notification.SalesBillId,
                "SalesBill",
                notification.UserId),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error ?? "Error al registrar salida de inventario por venta.");
    }
}
