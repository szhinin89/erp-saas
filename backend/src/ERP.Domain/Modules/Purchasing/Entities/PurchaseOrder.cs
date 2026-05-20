using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseOrder : AuditableEntity, ISubscriberScopedEntity
{
    public const int NumberMaxLen  = 20;
    public const int StatusMaxLen  = 20;
    public const int CurrencyMaxLen = 10;
    public const int NotesMaxLen   = 1000;
    public const int AddressMaxLen = 500;

    private readonly List<PurchaseOrderLine> _lines = new();

    public int       Sequential        { get; private set; }
    public string    OrderNumber       { get; private set; } = null!;
    public Guid      SupplierId        { get; private set; }
    public DateTime  IssueDate         { get; private set; }
    public DateTime  RequiredDate      { get; private set; }
    public string    Status            { get; private set; } = "Draft";
    public string    Currency          { get; private set; } = "USD";
    public decimal   Subtotal          { get; private set; }
    public decimal   TaxTotal          { get; private set; }
    public decimal   Total             { get; private set; }
    public string?   Notes             { get; private set; }
    public string?   DeliveryAddress   { get; private set; }
    public Guid?     TargetWarehouseId { get; private set; }
    public DateTime? SentAt            { get; private set; }
    public DateTime? ApprovedAt        { get; private set; }
    public Guid?     ApprovedBy        { get; private set; }
    public DateTime? ClosedAt          { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();

    private PurchaseOrder() { }

    public static PurchaseOrder Create(
        Guid      subscriberId,
        int       sequential,
        Guid      supplierId,
        DateTime  requiredDate,
        Guid?     targetWarehouseId,
        string?   deliveryAddress,
        string?   notes,
        Guid      createdBy)
    {
        var o = new PurchaseOrder
        {
            Id                = Guid.NewGuid(),
            SubscriberId          = subscriberId,
            Sequential        = sequential,
            OrderNumber       = $"PO-{sequential:D4}",
            SupplierId        = supplierId,
            IssueDate         = DateTime.UtcNow,
            RequiredDate      = requiredDate,
            Status            = "Draft",
            Currency          = "USD",
            TargetWarehouseId = targetWarehouseId,
            DeliveryAddress   = string.IsNullOrWhiteSpace(deliveryAddress) ? null : deliveryAddress.Trim(),
            Notes             = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        o.SetCreated(createdBy);
        return o;
    }

    public void AddLine(PurchaseOrderLine line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
        RecalcTotals();
    }

    public void RecalcTotals()
    {
        Subtotal = _lines.Sum(d => d.Subtotal);
        TaxTotal = _lines.Sum(d => d.TaxAmount);
        Total    = _lines.Sum(d => d.Total);
    }

    public void Send(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft POs can be sent (current: {Status}).");
        Status = "Sent";
        SentAt = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Approve(Guid userId)
    {
        if (Status is not ("Draft" or "Sent"))
            throw new InvalidOperationException($"Only Draft or Sent POs can be approved (current: {Status}).");
        Status     = "Approved";
        ApprovedAt = DateTime.UtcNow;
        ApprovedBy = userId;
        SetUpdated(userId);
    }

    public void Cancel(Guid userId)
    {
        if (Status is "Closed" or "Cancelled")
            throw new InvalidOperationException($"Cannot cancel a PO in status {Status}.");
        Status = "Cancelled";
        SetUpdated(userId);
    }

    public void MarkPartiallyReceived(Guid userId)
    {
        if (Status != "Approved") return;
        Status = "PartiallyReceived";
        SetUpdated(userId);
    }

    public void Close(Guid userId)
    {
        if (Status is not ("Approved" or "PartiallyReceived"))
            throw new InvalidOperationException($"Only Approved or PartiallyReceived POs can be closed (current: {Status}).");
        Status   = "Closed";
        ClosedAt = DateTime.UtcNow;
        SetUpdated(userId);
    }
}
