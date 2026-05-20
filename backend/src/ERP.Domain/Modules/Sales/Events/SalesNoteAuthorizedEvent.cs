using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

public sealed record SalesNoteStockLine(Guid ProductId, decimal Quantity);

public sealed class SalesNoteAuthorizedEvent : IDomainEvent
{
    public Guid     Id          { get; } = Guid.NewGuid();
    public DateTime OccurredOn  { get; } = DateTime.UtcNow;
    public Guid     NoteId      { get; }
    public Guid     SubscriberId    { get; }
    public Guid     UserId      { get; }
    public Guid     WarehouseId { get; }
    public Guid     CompanyId   { get; }
    public string   NoteNumber  { get; }
    public IReadOnlyList<SalesNoteStockLine> StockLines { get; }

    public SalesNoteAuthorizedEvent(
        Guid   noteId,
        Guid   subscriberId,
        Guid   userId,
        Guid   warehouseId,
        Guid   companyId,
        string noteNumber,
        IReadOnlyList<SalesNoteStockLine> stockLines)
    {
        NoteId      = noteId;
        SubscriberId    = subscriberId;
        UserId      = userId;
        WarehouseId = warehouseId;
        CompanyId   = companyId;
        NoteNumber  = noteNumber;
        StockLines  = stockLines;
    }
}
