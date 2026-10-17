using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

public sealed class JournalEntryAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public string SnapshotJson { get; private set; } = null!;
    private JournalEntryAudit() { }
    public static JournalEntryAudit Create(AuditActor actor, JournalEntry entry,
        string action, string snapshotJson, string? reason = null)
    {
        var audit = new JournalEntryAudit { CompanyId = entry.CompanyId, SnapshotJson = snapshotJson };
        audit.SetCommon(actor, entry.Id, action, reason);
        return audit;
    }
}
