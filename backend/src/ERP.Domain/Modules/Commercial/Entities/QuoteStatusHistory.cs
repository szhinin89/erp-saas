namespace ERP.Domain.Modules.Commercial.Entities;

public sealed class QuoteStatusHistory
{
    public const int StatusMaxLen = 20;

    public long Id { get; private set; }
    public long QuoteId { get; private set; }
    public Guid SubscriberId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = null!;
    public string? Reason { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateOnly IssueDate { get; private set; }

    private QuoteStatusHistory() { }

    public static QuoteStatusHistory Create(
        Guid subscriberId,
        long quoteId,
        DateOnly issueDate,
        string? fromStatus,
        string toStatus,
        string? reason,
        Guid changedBy)
    {
        return new QuoteStatusHistory
        {
            QuoteId = quoteId,
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

    public void AssignSnowflakeId(long id) => AssignId(id);
}
