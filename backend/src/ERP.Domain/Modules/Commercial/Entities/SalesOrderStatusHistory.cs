namespace ERP.Domain.Modules.Commercial.Entities;

public sealed class SalesOrderStatusHistory
{
    public const int StatusMaxLen = 20;

    public long Id { get; private set; }
    public long SalesOrderId { get; private set; }
    public Guid SubscriberId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = null!;
    public string? Reason { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateOnly IssueDate { get; private set; }

    private SalesOrderStatusHistory() { }

    public static SalesOrderStatusHistory Create(
        Guid subscriberId,
        long salesOrderId,
        DateOnly issueDate,
        string? fromStatus,
        string toStatus,
        string? reason,
        Guid changedBy)
    {
        return CreateWithId(
            0,
            subscriberId,
            salesOrderId,
            issueDate,
            fromStatus,
            toStatus,
            reason,
            changedBy);
    }

    public static SalesOrderStatusHistory CreateWithId(
        long id,
        Guid subscriberId,
        long salesOrderId,
        DateOnly issueDate,
        string? fromStatus,
        string toStatus,
        string? reason,
        Guid changedBy)
    {
        var history = new SalesOrderStatusHistory
        {
            SalesOrderId = salesOrderId,
            SubscriberId = subscriberId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy,
            IssueDate = issueDate,
        };

        if (id != 0)
            history.AssignId(id);

        return history;
    }

    internal void AssignId(long id) => Id = id;

    public void AssignSnowflakeId(long id) => AssignId(id);
}
