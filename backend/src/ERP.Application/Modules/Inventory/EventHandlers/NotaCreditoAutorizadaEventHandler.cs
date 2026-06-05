using ERP.Application.Common.Inventory;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Inventory.EventHandlers;

public sealed class SalesNoteAuthorizedEventHandler : INotificationHandler<SalesNoteAuthorizedEvent>
{
    private readonly IInventoryPostingService _posting;

    public SalesNoteAuthorizedEventHandler(IInventoryPostingService posting) => _posting = posting;

    public async Task Handle(SalesNoteAuthorizedEvent notification, CancellationToken ct)
    {
        if (notification.CompanyId == Guid.Empty || notification.StockLines.Count == 0)
            return;

        var lines = notification.StockLines
            .Select(l => new InventoryPostingLine(l.ProductId, l.Quantity))
            .ToList();

        var result = await _posting.PostSaleReturnAsync(
            new InventoryPostingRequest(
                notification.SubscriberId,
                notification.CompanyId,
                notification.WarehouseId,
                lines,
                notification.NoteNumber,
                notification.NoteId,
                "SalesNote",
                notification.UserId),
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error ?? "Error al registrar reingreso de inventario por note.");
    }
}
