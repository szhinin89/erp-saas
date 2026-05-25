using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Events;

namespace ERP.Domain.Modules.Commercial.Entities;

public sealed class SalesOrder : SnowflakeAuditableEntity
{
    public const int NumberMaxLen = 30;
    public const int StatusMaxLen = 20;
    public const int CurrencyMaxLen = 3;
    public const int NotesMaxLen = 2000;

    public static class Statuses
    {
        public const string Draft = "Draft";
        public const string Confirmed = "Confirmed";
        public const string PartiallyInvoiced = "PartiallyInvoiced";
        public const string Invoiced = "Invoiced";
        public const string Cancelled = "Cancelled";
    }

    private readonly List<SalesOrderDetail> _lines = new();
    private readonly List<SalesOrderStatusHistory> _statusHistory = new();

    public Guid BranchId { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public Guid BusinessPartnerId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly? RequiredDate { get; private set; }
    public string Status { get; private set; } = Statuses.Draft;
    public string CurrencyCode { get; private set; } = "USD";
    public decimal Subtotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal Total { get; private set; }
    public short PaymentTermDays { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyList<SalesOrderDetail> Lines => _lines.AsReadOnly();
    public IReadOnlyList<SalesOrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private SalesOrder() { }

    public static SalesOrder Create(
        long id,
        Guid publicId,
        Guid subscriberId,
        Guid branchId,
        string orderNumber,
        Guid businessPartnerId,
        Guid warehouseId,
        DateOnly issueDate,
        DateOnly? requiredDate,
        short paymentTermDays,
        string? notes,
        Guid createdBy)
    {
        if (requiredDate.HasValue && requiredDate.Value < issueDate)
            throw new ArgumentException("RequiredDate must be on or after IssueDate.");

        var order = new SalesOrder
        {
            Id = id,
            PublicId = publicId,
            SubscriberId = subscriberId,
            CompanyId = branchId,
            BranchId = branchId,
            OrderNumber = orderNumber.Trim(),
            BusinessPartnerId = businessPartnerId,
            WarehouseId = warehouseId,
            IssueDate = issueDate,
            RequiredDate = requiredDate,
            PaymentTermDays = paymentTermDays,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = Statuses.Draft,
        };
        order.SetCreated(createdBy);
        order.RecordStatus(null, Statuses.Draft, null, createdBy);
        order.RaiseDomainEvent(new SalesOrderCreatedEvent(
            order.Id, order.PublicId, subscriberId, branchId, warehouseId, createdBy));
        return order;
    }

    public void AddLine(SalesOrderDetail line)
    {
        if (Status != Statuses.Draft)
            throw new InvalidOperationException("Only draft orders can be modified.");
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
        RecalcTotals();
    }

    public void RecalcTotals()
    {
        Subtotal = _lines.Sum(l => l.LineSubtotal);
        TaxTotal = _lines.Sum(l => l.LineTax);
        Total = _lines.Sum(l => l.LineTotal);
    }

    public void Confirm(Guid userId)
    {
        Transition(Statuses.Confirmed, userId, null);
        RaiseDomainEvent(new SalesOrderConfirmedEvent(
            Id, PublicId, SubscriberId, BranchId, WarehouseId, userId));
    }

    public void Cancel(Guid userId, string? reason)
    {
        if (Status is Statuses.Invoiced or Statuses.PartiallyInvoiced)
            throw new InvalidOperationException($"Cannot cancel order in status {Status}.");
        Transition(Statuses.Cancelled, userId, reason);
    }

    public void MarkPartiallyInvoiced(Guid userId)
    {
        if (Status != Statuses.Confirmed && Status != Statuses.PartiallyInvoiced)
            throw new InvalidOperationException($"Invalid status transition from {Status}.");
        Transition(Statuses.PartiallyInvoiced, userId, null);
    }

    public void MarkInvoiced(Guid userId)
    {
        if (Status != Statuses.Confirmed && Status != Statuses.PartiallyInvoiced)
            throw new InvalidOperationException($"Invalid status transition from {Status}.");
        Transition(Statuses.Invoiced, userId, null);
    }

    private void Transition(string toStatus, Guid userId, string? reason)
    {
        var from = Status;
        Status = toStatus;
        SetUpdated(userId);
        RecordStatus(from, toStatus, reason, userId);
    }

    private void RecordStatus(string? from, string to, string? reason, Guid userId)
    {
        _statusHistory.Add(SalesOrderStatusHistory.Create(
            SubscriberId,
            Id,
            IssueDate,
            from,
            to,
            reason,
            userId));
    }

    public void AssignSnowflakeChildIds(ISnowflakeIdGenerator idGenerator)
    {
        foreach (var line in _lines)
        {
            if (line.Id == 0)
                line.AssignId(idGenerator.NextId());
        }

        foreach (var history in _statusHistory)
        {
            if (history.Id == 0)
                history.AssignId(idGenerator.NextId());
        }
    }
}
