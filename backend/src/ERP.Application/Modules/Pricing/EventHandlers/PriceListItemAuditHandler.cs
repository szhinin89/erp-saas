using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Events;
using MediatR;

namespace ERP.Application.Modules.Pricing.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="PriceListItem"/> a <see cref="PriceListItemAudit"/>.
/// Mismo patrón que <c>PricingRuleAuditHandler</c> — único lugar que conoce ambos lados.
/// </summary>
public sealed class PriceListItemAuditHandler :
    INotificationHandler<PriceListItemAssignedEvent>,
    INotificationHandler<PriceListItemEnabledEvent>,
    INotificationHandler<PriceListItemDisabledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public PriceListItemAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(PriceListItemAssignedEvent e, CancellationToken ct) => _audit.RecordAsync(
        PriceListItemAudit.Create(
            _context.Actor, _context.CompanyId, e.AssignmentId, e.PriceListId, e.ItemId, ((IAuditEvent)e).Action),
        ct);

    public Task Handle(PriceListItemEnabledEvent e, CancellationToken ct) => _audit.RecordAsync(
        PriceListItemAudit.Create(
            _context.Actor, _context.CompanyId, e.AssignmentId, e.PriceListId, e.ItemId, ((IAuditEvent)e).Action),
        ct);

    public Task Handle(PriceListItemDisabledEvent e, CancellationToken ct) => _audit.RecordAsync(
        PriceListItemAudit.Create(
            _context.Actor, _context.CompanyId, e.AssignmentId, e.PriceListId, e.ItemId, ((IAuditEvent)e).Action),
        ct);
}
