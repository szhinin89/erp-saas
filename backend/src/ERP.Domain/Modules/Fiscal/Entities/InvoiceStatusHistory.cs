namespace ERP.Domain.Modules.Fiscal.Entities;

public sealed class InvoiceStatusHistory
{
    public const int StatusMaxLen = 20;

    public long Id { get; private set; }
    public long InvoiceId { get; private set; }
    public Guid SubscriberId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = null!;
    public string? Reason { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateOnly IssueDate { get; private set; }

    private InvoiceStatusHistory() { }

    public static InvoiceStatusHistory Create(
        Guid subscriberId,
        long invoiceId,
        DateOnly issueDate,
        string? fromStatus,
        string toStatus,
        string? reason,
        Guid changedBy)
    {
        return new InvoiceStatusHistory
        {
            InvoiceId = invoiceId,
            SubscriberId = subscriberId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy,
            IssueDate = issueDate,
        };
    }

    internal void AssignId(long id) => Id = id;
}
