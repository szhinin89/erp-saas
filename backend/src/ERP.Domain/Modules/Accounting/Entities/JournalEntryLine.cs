using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Domain.Modules.Accounting.Entities;

public class JournalEntryLine : BaseEntity
{
    public Guid JournalEntryId { get; private set; }
    public Guid AccountId { get; private set; }
    public Money Debit { get; private set; } = null!;
    public Money Credit { get; private set; } = null!;

    private JournalEntryLine() { }

    internal static JournalEntryLine Create(
        Guid journalEntryId,
        Guid tenantId,
        Guid accountId,
        Money debit,
        Money credit)
    {
        return new JournalEntryLine
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            JournalEntryId = journalEntryId,
            AccountId      = accountId,
            Debit          = debit,
            Credit         = credit
        };
    }
}
