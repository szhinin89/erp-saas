using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesRetention : AuditableEntity, ISubscriberScopedEntity
{
    public const int VoucherTypeMaxLen = 30;
    public const int AccessKeyMaxLen   = 49;
    public const int StatusMaxLen      = 20;
    public const int XmlPathMaxLen     = 500;

    private readonly List<SalesRetentionLine> _lines = new();

    public Guid      BusinessPartnerId { get; private set; }
    public string    VoucherType    { get; private set; } = "RETENTION";
    public string    AccessKey      { get; private set; } = null!;
    public DateTime  IssueDate      { get; private set; }
    public decimal   TotalRetained  { get; private set; }
    public string    Status         { get; private set; } = "Active";
    public Guid?     SalesBillId    { get; private set; }
    public string?   XmlPath        { get; private set; }
    public Guid?     JournalEntryId { get; private set; }

    public IReadOnlyList<SalesRetentionLine> Lines => _lines.AsReadOnly();

    private SalesRetention() { }

    public static SalesRetention Create(
        Guid     subscriberId,
        Guid     businessPartnerId,
        string   accessKey,
        DateTime issueDate,
        decimal  totalRetained,
        Guid?    salesBillId,
        string?  xmlPath,
        Guid     createdBy)
    {
        var r = new SalesRetention
        {
            Id             = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            BusinessPartnerId = businessPartnerId,
            AccessKey      = accessKey.Trim(),
            IssueDate      = issueDate,
            TotalRetained  = totalRetained,
            SalesBillId    = salesBillId,
            XmlPath        = string.IsNullOrWhiteSpace(xmlPath) ? null : xmlPath.Trim(),
        };
        r.SetCreated(createdBy);
        return r;
    }

    public void AddLine(SalesRetentionLine line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
    }

    public void LinkJournalEntry(Guid journalEntryId, Guid userId)
    {
        JournalEntryId = journalEntryId;
        SetUpdated(userId);
    }
}
