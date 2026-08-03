using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Finance.Events;

/// <summary>
/// Se levanta cuando <c>CompanyFinancialDestination.ChangeAccountingAccount()</c> cambia
/// <c>AccountingAccountId</c> (diseño §6.4bis/§6.4ter, §20.1). Solo afecta operaciones futuras —
/// cada reembolso ya confirmado congela su propia cuenta en <c>SupplierCreditRefundTransaction</c>.
/// </summary>
public sealed class CompanyFinancialDestinationAccountChangedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid DestinationId { get; }
    public string Code { get; }
    public Guid OldAccountingAccountId { get; }
    public Guid NewAccountingAccountId { get; }

    public CompanyFinancialDestinationAccountChangedEvent(
        Guid tenantId,
        Guid destinationId,
        string code,
        Guid oldAccountingAccountId,
        Guid newAccountingAccountId
    )
    {
        TenantId = tenantId;
        DestinationId = destinationId;
        Code = code;
        OldAccountingAccountId = oldAccountingAccountId;
        NewAccountingAccountId = newAccountingAccountId;
    }

    Guid IAuditEvent.EntityId => DestinationId;
    string IAuditEvent.Action => "AccountChanged";
    string? IAuditEvent.Reason => null;
}
