using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

public sealed record SalesNoteStockLine(Guid ProductId, decimal Quantity);

public sealed class SalesNoteAuthorizedEvent : IDomainEvent
{
    public Guid     Id          { get; } = Guid.NewGuid();
    public DateTime OccurredOn  { get; } = DateTime.UtcNow;
    public Guid     NoteId      { get; }
    public Guid     TenantId    { get; }
    public Guid     UserId      { get; }
    public Guid     WarehouseId { get; }
    public string   NoteNumber  { get; }
    public IReadOnlyList<SalesNoteStockLine> StockLines { get; }

    public SalesNoteAuthorizedEvent(
        Guid   noteId,
        Guid   tenantId,
        Guid   userId,
        Guid   warehouseId,
        string noteNumber,
        IReadOnlyList<SalesNoteStockLine> stockLines)
    {
        NoteId      = noteId;
        TenantId    = tenantId;
        UserId      = userId;
        WarehouseId = warehouseId;
        NoteNumber  = noteNumber;
        StockLines  = stockLines;
    }
}
