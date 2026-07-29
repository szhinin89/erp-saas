using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Events;
using MediatR;

namespace ERP.Application.Modules.Pricing.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="PriceList"/> a <see cref="PriceListAudit"/>.
/// Mismo patrón que <c>PricingRuleAuditHandler</c>/<c>PriceListItemAuditHandler</c> — único
/// lugar que conoce ambos lados (evento de dominio ↔ entidad de auditoría).
/// </summary>
public sealed class PriceListAuditHandler
    : INotificationHandler<PriceListCreatedEvent>,
        INotificationHandler<PriceListUpdatedEvent>,
        INotificationHandler<PriceListEnabledEvent>,
        INotificationHandler<PriceListDisabledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public PriceListAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(PriceListCreatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PriceListAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.PriceListId,
                ((IAuditEvent)e).Action
            ),
            ct
        );

    public Task Handle(PriceListUpdatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PriceListAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.PriceListId,
                ((IAuditEvent)e).Action,
                e.OldRuleType,
                e.OldRuleValue,
                e.NewRuleType,
                e.NewRuleValue
            ),
            ct
        );

    public Task Handle(PriceListEnabledEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PriceListAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.PriceListId,
                ((IAuditEvent)e).Action
            ),
            ct
        );

    public Task Handle(PriceListDisabledEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PriceListAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.PriceListId,
                ((IAuditEvent)e).Action
            ),
            ct
        );
}
