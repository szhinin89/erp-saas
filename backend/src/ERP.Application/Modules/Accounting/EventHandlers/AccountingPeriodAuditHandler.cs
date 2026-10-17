using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.EventHandlers;

/// <summary>
/// ACCOUNTING-07 — traduce los eventos de <see cref="AccountingPeriod"/> a
/// <see cref="AccountingPeriodAudit"/> (ADR-022, Entity Audit). Tenant y Company salen del evento,
/// no del request: el período también se crea en el bootstrap de una Company nueva, donde el
/// contexto ambiente puede pertenecer a otra Company o no tener tenant (job/backfill).
/// </summary>
public sealed class AccountingPeriodAuditHandler
    : INotificationHandler<AccountingPeriodCreatedEvent>,
        INotificationHandler<AccountingPeriodClosedEvent>,
        INotificationHandler<AccountingPeriodLockedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public AccountingPeriodAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(AccountingPeriodCreatedEvent e, CancellationToken ct) => Record(e, ct);

    public Task Handle(AccountingPeriodClosedEvent e, CancellationToken ct) => Record(e, ct);

    public Task Handle(AccountingPeriodLockedEvent e, CancellationToken ct) => Record(e, ct);

    private Task Record(AccountingPeriodEventBase e, CancellationToken ct) =>
        _audit.RecordAsync(
            AccountingPeriodAudit.Create(
                _context.Actor with { TenantId = e.TenantId ?? _context.TenantId },
                e.CompanyId,
                e.AccountingPeriodId,
                ((IAuditEvent)e).Action,
                e.FiscalYear,
                e.PeriodNumber,
                e.StartDate,
                e.EndDate
            ),
            ct
        );
}
