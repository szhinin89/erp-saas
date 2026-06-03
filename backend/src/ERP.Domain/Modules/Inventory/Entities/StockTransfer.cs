using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class StockTransfer : AuditableEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public const int NumberMaxLen = 20;
    public const int StatusMaxLen = 20;
    public const int ReasonMaxLen = 500;
    public const int NotesMaxLen  = 1000;

    private readonly List<StockTransferLine> _lines = new();

    public int       Sequential        { get; private set; }
    public string    TransferNumber    { get; private set; } = null!;
    public Guid      SourceWarehouseId { get; private set; }
    public Guid      TargetWarehouseId { get; private set; }
    public DateTime  TransferDate      { get; private set; }
    public string    Status            { get; private set; } = "Draft";
    public string?   Reason            { get; private set; }
    public string?   Notes             { get; private set; }
    public DateTime? ConfirmedAt       { get; private set; }
    public Guid?     ConfirmedBy       { get; private set; }

    public Warehouse SourceWarehouse { get; private set; } = null!;
    public Warehouse TargetWarehouse { get; private set; } = null!;

    public IReadOnlyList<StockTransferLine> Lines => _lines.AsReadOnly();

    private StockTransfer() { }

    public static StockTransfer Create(
        Guid    subscriberId,
        int     sequential,
        Guid    sourceWarehouseId,
        Guid    targetWarehouseId,
        string? reason,
        string? notes,
        Guid    createdBy,
        Guid companyId = default)
    {
        var t = new StockTransfer
        {
            Id                = Guid.NewGuid(),
            SubscriberId          = subscriberId,
            CompanyId = companyId,
            Sequential        = sequential,
            TransferNumber    = $"TR-{sequential:D4}",
            SourceWarehouseId = sourceWarehouseId,
            TargetWarehouseId = targetWarehouseId,
            TransferDate      = DateTime.UtcNow,
            Status            = "Draft",
            Reason            = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Notes             = string.IsNullOrWhiteSpace(notes)  ? null : notes.Trim(),
        };
        t.SetCreated(createdBy);
        return t;
    }

    public void AddLine(StockTransferLine line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
    }

    public void Confirm(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft transfers can be confirmed (current: {Status}).");
        Status      = "Confirmed";
        ConfirmedAt = DateTime.UtcNow;
        ConfirmedBy = userId;
        SetUpdated(userId);
    }

    public void Cancel(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft transfers can be cancelled (current: {Status}).");
        Status = "Cancelled";
        SetUpdated(userId);
    }
}
