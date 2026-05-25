using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Events;

namespace ERP.Domain.Modules.Commercial.Entities;

public sealed class Quote : SnowflakeAuditableEntity
{
    public const int NumberMaxLen = 30;
    public const int StatusMaxLen = 20;
    public const int CurrencyMaxLen = 3;
    public const int NotesMaxLen = 2000;

    public static class Statuses
    {
        public const string Draft = "Draft";
        public const string Sent = "Sent";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";
        public const string Converted = "Converted";
    }

    private readonly List<QuoteDetail> _lines = new();
    private readonly List<QuoteStatusHistory> _statusHistory = new();

    public Guid BranchId { get; private set; }
    public string QuoteNumber { get; private set; } = null!;
    public Guid BusinessPartnerId { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public string Status { get; private set; } = Statuses.Draft;
    public string CurrencyCode { get; private set; } = "USD";
    public decimal Subtotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal Total { get; private set; }
    public short PaymentTermDays { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyList<QuoteDetail> Lines => _lines.AsReadOnly();
    public IReadOnlyList<QuoteStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private Quote() { }

    public static Quote Create(
        long id,
        Guid publicId,
        Guid subscriberId,
        Guid branchId,
        string quoteNumber,
        Guid businessPartnerId,
        DateOnly issueDate,
        DateOnly validUntil,
        short paymentTermDays,
        string? notes,
        Guid createdBy)
    {
        if (validUntil < issueDate)
            throw new ArgumentException("ValidUntil must be on or after IssueDate.");

        var quote = new Quote
        {
            Id = id,
            PublicId = publicId,
            SubscriberId = subscriberId,
            CompanyId = branchId,
            BranchId = branchId,
            QuoteNumber = quoteNumber.Trim(),
            BusinessPartnerId = businessPartnerId,
            IssueDate = issueDate,
            ValidUntil = validUntil,
            PaymentTermDays = paymentTermDays,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = Statuses.Draft,
        };
        quote.SetCreated(createdBy);
        quote.RecordStatus(null, Statuses.Draft, null, createdBy);
        quote.RaiseDomainEvent(new QuoteCreatedEvent(quote.Id, quote.PublicId, subscriberId, branchId, createdBy));
        return quote;
    }

    public void AddLine(QuoteDetail line)
    {
        if (Status != Statuses.Draft)
            throw new InvalidOperationException("Only draft quotes can be modified.");
        if (line is null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
        RecalcTotals();
    }

    public void RecalcTotals()
    {
        Subtotal = _lines.Sum(l => l.LineSubtotal);
        TaxTotal = _lines.Sum(l => l.LineTax);
        Total = _lines.Sum(l => l.LineTotal);
    }

    public void MarkSent(Guid userId)
    {
        Transition(Statuses.Sent, userId, null);
    }

    public void Approve(Guid userId)
    {
        if (DateOnly.FromDateTime(DateTime.UtcNow) > ValidUntil)
            throw new InvalidOperationException("Quote has expired.");
        Transition(Statuses.Approved, userId, null);
    }

    public void Reject(Guid userId, string? reason)
    {
        Transition(Statuses.Rejected, userId, reason);
    }

    public void Cancel(Guid userId, string? reason)
    {
        Transition(Statuses.Cancelled, userId, reason);
    }

    public void MarkConverted(Guid userId)
    {
        Transition(Statuses.Converted, userId, null);
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
        _statusHistory.Add(QuoteStatusHistory.Create(
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
