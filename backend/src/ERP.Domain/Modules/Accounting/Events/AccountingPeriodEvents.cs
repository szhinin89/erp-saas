using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Events;

/// <summary>Se levanta cuando <c>AccountingPeriod.Create()</c> abre un nuevo período contable.</summary>
public sealed class AccountingPeriodCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AccountingPeriodId { get; }
    public Guid CompanyId { get; }

    public AccountingPeriodCreatedEvent(Guid tenantId, Guid accountingPeriodId, Guid companyId)
    {
        TenantId = tenantId;
        AccountingPeriodId = accountingPeriodId;
        CompanyId = companyId;
    }

    Guid IAuditEvent.EntityId => AccountingPeriodId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>AccountingPeriod.Close()</c> cierra el período (ADR-026 §6.1 — bloquea Post() posteriores).</summary>
public sealed class AccountingPeriodClosedEvent : BaseDomainEvent
{
    public Guid AccountingPeriodId { get; }
    public Guid CompanyId { get; }

    public AccountingPeriodClosedEvent(Guid tenantId, Guid accountingPeriodId, Guid companyId)
    {
        TenantId = tenantId;
        AccountingPeriodId = accountingPeriodId;
        CompanyId = companyId;
    }
}

/// <summary>Se levanta cuando <c>AccountingPeriod.Lock()</c> bloquea definitivamente un período ya Closed.</summary>
public sealed class AccountingPeriodLockedEvent : BaseDomainEvent
{
    public Guid AccountingPeriodId { get; }
    public Guid CompanyId { get; }

    public AccountingPeriodLockedEvent(Guid tenantId, Guid accountingPeriodId, Guid companyId)
    {
        TenantId = tenantId;
        AccountingPeriodId = accountingPeriodId;
        CompanyId = companyId;
    }
}
