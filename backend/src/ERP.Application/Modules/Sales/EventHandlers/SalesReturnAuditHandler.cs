using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Sales.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="SalesReturn"/> a <see cref="SalesReturnAudit"/>.
/// Único lugar que conoce ambos lados (evento de dominio ↔ entidad de auditoría) — el resto de
/// la infraestructura de auditoría es genérica y no conoce Sales. Mismo patrón que
/// <c>PurchaseInvoiceAuditHandler</c>/<c>PricingRuleAuditHandler</c>.
/// </summary>
public sealed class SalesReturnAuditHandler
    : INotificationHandler<SalesReturnDraftCreatedEvent>,
        INotificationHandler<SalesReturnAuthorizedEvent>,
        INotificationHandler<SalesReturnCancelledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public SalesReturnAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(SalesReturnDraftCreatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            SalesReturnAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.SalesReturnId,
                e.SalesInvoiceId,
                e.ReturnNumber,
                ((IAuditEvent)e).Action,
                customerId: e.CustomerId,
                reason: ((IAuditEvent)e).Reason
            ),
            ct
        );

    public Task Handle(SalesReturnAuthorizedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            SalesReturnAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.SalesReturnId,
                e.SalesInvoiceId,
                e.ReturnNumber,
                ((IAuditEvent)e).Action,
                grandTotal: e.GrandTotal,
                reason: ((IAuditEvent)e).Reason
            ),
            ct
        );

    public Task Handle(SalesReturnCancelledEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            SalesReturnAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.SalesReturnId,
                e.SalesInvoiceId,
                e.ReturnNumber,
                ((IAuditEvent)e).Action,
                customerId: e.CustomerId,
                reason: ((IAuditEvent)e).Reason
            ),
            ct
        );
}
