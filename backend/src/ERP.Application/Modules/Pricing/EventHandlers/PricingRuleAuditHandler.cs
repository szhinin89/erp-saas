using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Events;
using MediatR;

namespace ERP.Application.Modules.Pricing.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="PricingRule"/> a <see cref="PricingRuleAudit"/>.
/// Único lugar que conoce ambos lados (evento de dominio ↔ entidad de auditoría) — el
/// resto de la infraestructura de auditoría es genérica y no conoce Pricing.
/// </summary>
public sealed class PricingRuleAuditHandler :
    INotificationHandler<PricingRuleUpdatedEvent>,
    INotificationHandler<PricingRuleEnabledEvent>,
    INotificationHandler<PricingRuleDisabledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public PricingRuleAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(PricingRuleUpdatedEvent e, CancellationToken ct) => _audit.RecordAsync(
        PricingRuleAudit.Create(
            _context.Actor, _context.CompanyId, e.RuleId, e.PriceListId, e.ItemId, ((IAuditEvent)e).Action,
            e.OldRuleType, e.OldRuleValue, e.NewRuleType, e.NewRuleValue),
        ct);

    public Task Handle(PricingRuleEnabledEvent e, CancellationToken ct) => _audit.RecordAsync(
        PricingRuleAudit.Create(
            _context.Actor, _context.CompanyId, e.RuleId, e.PriceListId, e.ItemId, ((IAuditEvent)e).Action,
            e.RuleType, e.RuleValue, e.RuleType, e.RuleValue),
        ct);

    public Task Handle(PricingRuleDisabledEvent e, CancellationToken ct) => _audit.RecordAsync(
        PricingRuleAudit.Create(
            _context.Actor, _context.CompanyId, e.RuleId, e.PriceListId, e.ItemId, ((IAuditEvent)e).Action,
            e.RuleType, e.RuleValue, e.RuleType, e.RuleValue),
        ct);
}
