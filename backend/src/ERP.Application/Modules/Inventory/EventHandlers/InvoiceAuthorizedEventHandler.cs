using ERP.Application.Common.Inventory;
using ERP.Domain.Modules.Fiscal.Events;
using MediatR;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class InvoiceAuthorizedEventHandler : INotificationHandler<InvoiceAuthorizedEvent>
{
    private readonly IInventoryPostingService _posting;

    public InvoiceAuthorizedEventHandler(IInventoryPostingService posting) => _posting = posting;

    public async Task Handle(InvoiceAuthorizedEvent notification, CancellationToken ct)
    {
        if (notification.BranchId == Guid.Empty || notification.StockLines.Count == 0)
            return;

        var lines = notification.StockLines
            .Select(l => new InventoryPostingLine(l.ProductId, l.Quantity))
            .ToList();

        var result = await _posting.PostSaleExitAsync(
            new InventoryPostingRequest(
                notification.SubscriberId,
                notification.BranchId,
                notification.WarehouseId,
                lines,
                notification.InvoiceNumber,
                notification.InvoicePublicId,
                "Fiscal.Invoice",
                notification.UserId),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error ?? "Error al registrar salida de inventario por salesBill.");
    }
}
