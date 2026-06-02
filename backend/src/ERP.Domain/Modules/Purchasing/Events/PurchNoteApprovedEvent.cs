using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Events;

public sealed record PurchNoteStockLine(
    Guid    ProductId,
    Guid    WarehouseId,
    decimal QuantityDelta,
    decimal UnitCost);

public sealed class PurchNoteApprovedEvent : IDomainEvent
{
    public Guid     Id          { get; } = Guid.NewGuid();
    public DateTime OccurredOn  { get; } = DateTime.UtcNow;
    public Guid     NoteId      { get; }
    public Guid     SubscriberId    { get; }
    public Guid     UserId      { get; }
    public Guid     PurchBillId { get; }
    public NoteType NoteType    { get; }
    public string   NoteNumber  { get; }
    public IReadOnlyList<PurchNoteStockLine> StockLines { get; }

    public PurchNoteApprovedEvent(
        Guid     noteId,
        Guid     subscriberId,
        Guid     userId,
        Guid     purchBillId,
        NoteType noteType,
        string   noteNumber,
        IReadOnlyList<PurchNoteStockLine> stockLines)
    {
        NoteId      = noteId;
        SubscriberId    = subscriberId;
        UserId      = userId;
        PurchBillId = purchBillId;
        NoteType    = noteType;
        NoteNumber  = noteNumber;
        StockLines  = stockLines;
    }
}
