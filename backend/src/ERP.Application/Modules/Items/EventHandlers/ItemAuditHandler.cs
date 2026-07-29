using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Events;
using MediatR;

namespace ERP.Application.Modules.Items.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="Item"/> a <see cref="ItemAudit"/>. Único lugar
/// que conoce ambos lados (evento de dominio ↔ entidad de auditoría) — el resto de la
/// infraestructura de auditoría (IAuditService/IAuditWriter/IAuditContext) es genérica y no
/// conoce Items. Mismo patrón que PricingRuleAuditHandler/PriceListItemAuditHandler.
/// </summary>
public sealed class ItemAuditHandler
    : INotificationHandler<ItemCreatedEvent>,
        INotificationHandler<ItemUpdatedEvent>,
        INotificationHandler<ItemPriceChangedEvent>,
        INotificationHandler<ItemEnabledEvent>,
        INotificationHandler<ItemDisabledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public ItemAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(ItemCreatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(ItemAudit.Create(_context.Actor, e.ItemId, ((IAuditEvent)e).Action), ct);

    public Task Handle(ItemUpdatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(ItemAudit.Create(_context.Actor, e.ItemId, ((IAuditEvent)e).Action), ct);

    public Task Handle(ItemPriceChangedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            ItemAudit.Create(
                _context.Actor,
                e.ItemId,
                ((IAuditEvent)e).Action,
                e.OldBaseSalePrice,
                e.NewBaseSalePrice
            ),
            ct
        );

    public Task Handle(ItemEnabledEvent e, CancellationToken ct) =>
        _audit.RecordAsync(ItemAudit.Create(_context.Actor, e.ItemId, ((IAuditEvent)e).Action), ct);

    public Task Handle(ItemDisabledEvent e, CancellationToken ct) =>
        _audit.RecordAsync(ItemAudit.Create(_context.Actor, e.ItemId, ((IAuditEvent)e).Action), ct);
}
