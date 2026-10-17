using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Events;

/// <summary>
/// ACCOUNTING-07: base común de los eventos de <see cref="AccountingPeriod"/> — snapshot del período
/// (ejercicio, número y rango) para su Entity Audit (<c>AccountingPeriodAudit</c>, ADR-022).
/// </summary>
public abstract class AccountingPeriodEventBase : BaseDomainEvent, IAuditEvent
{
    public Guid AccountingPeriodId { get; }
    public Guid CompanyId { get; }
    public int FiscalYear { get; }
    public int PeriodNumber { get; }
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }

    protected AccountingPeriodEventBase(AccountingPeriod period)
    {
        TenantId = period.TenantId;
        AccountingPeriodId = period.Id;
        CompanyId = period.CompanyId;
        FiscalYear = period.FiscalYear;
        PeriodNumber = period.PeriodNumber;
        StartDate = period.StartDate;
        EndDate = period.EndDate;
    }

    protected abstract string AuditAction { get; }

    Guid IAuditEvent.EntityId => AccountingPeriodId;
    string IAuditEvent.Action => AuditAction;
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>AccountingPeriod.Create()</c> abre un nuevo período contable.</summary>
public sealed class AccountingPeriodCreatedEvent : AccountingPeriodEventBase
{
    public AccountingPeriodCreatedEvent(AccountingPeriod period)
        : base(period) { }

    protected override string AuditAction => "Created";
}

/// <summary>Se levanta cuando <c>AccountingPeriod.Close()</c> cierra el período (ADR-026 §6.1 — bloquea Post() posteriores).</summary>
public sealed class AccountingPeriodClosedEvent : AccountingPeriodEventBase
{
    public AccountingPeriodClosedEvent(AccountingPeriod period)
        : base(period) { }

    protected override string AuditAction => "Closed";
}

/// <summary>Se levanta cuando <c>AccountingPeriod.Lock()</c> bloquea definitivamente un período ya Closed.</summary>
public sealed class AccountingPeriodLockedEvent : AccountingPeriodEventBase
{
    public AccountingPeriodLockedEvent(AccountingPeriod period)
        : base(period) { }

    protected override string AuditAction => "Locked";
}
