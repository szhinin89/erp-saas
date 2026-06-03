using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class StockAdjustment : AuditableEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public const int NumberMaxLen          = 20;
    public const int AdjustmentTypeMaxLen  = 20;
    public const int ReasonMaxLen          = 200;
    public const int NotesMaxLen           = 1000;
    public const int StatusMaxLen          = 20;
    public const int NameSnapshotMaxLen    = 300;

    public int       Sequential       { get; private set; }
    public string    AdjustmentNumber { get; private set; } = null!;
    public Guid      WarehouseId      { get; private set; }
    public string    WarehouseName    { get; private set; } = null!;
    public Guid      ProductId        { get; private set; }
    public string    ProductName      { get; private set; } = null!;
    public decimal   AdjustmentQty   { get; private set; }
    public string    AdjustmentType  { get; private set; } = null!;
    public string    Reason          { get; private set; } = null!;
    public string?   Notes           { get; private set; }
    public DateTime  AdjustmentDate  { get; private set; }
    public string    Status          { get; private set; } = "Draft";
    public DateTime? ExecutedAt      { get; private set; }
    public Guid?     ExecutedBy      { get; private set; }
    public ICollection<StockAdjustmentLine> Lines { get; set; } = [];

    private StockAdjustment() { }

    public static StockAdjustment Create(
        Guid    subscriberId,
        int     sequential,
        Guid    warehouseId,
        string  warehouseName,
        Guid    productId,
        string  productName,
        decimal adjustmentQty,
        string  reason,
        string? notes,
        Guid    createdBy,
        Guid companyId = default)
    {
        if (adjustmentQty == 0)
            throw new ArgumentException("Adjustment quantity cannot be zero.", nameof(adjustmentQty));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        var a = new StockAdjustment
        {
            Id               = Guid.NewGuid(),
            SubscriberId         = subscriberId,
            CompanyId = companyId,
            Sequential       = sequential,
            AdjustmentNumber = $"ADJ-{sequential:D4}",
            WarehouseId      = warehouseId,
            WarehouseName    = warehouseName.Trim(),
            ProductId        = productId,
            ProductName      = productName.Trim(),
            AdjustmentQty   = adjustmentQty,
            AdjustmentType  = adjustmentQty > 0 ? "Increment" : "Decrement",
            Reason          = reason.Trim(),
            Notes           = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            AdjustmentDate  = DateTime.UtcNow,
            Status          = "Draft",
        };
        a.SetCreated(createdBy);
        return a;
    }

    public void Execute(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft adjustments can be executed (current: {Status}).");
        Status     = "Executed";
        ExecutedAt = DateTime.UtcNow;
        ExecutedBy = userId;
        SetUpdated(userId);
    }

    public void Cancel(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft adjustments can be cancelled (current: {Status}).");
        Status = "Cancelled";
        SetUpdated(userId);
    }
}
