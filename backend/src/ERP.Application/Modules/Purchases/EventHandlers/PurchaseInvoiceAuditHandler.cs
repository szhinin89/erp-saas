using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Purchases.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="PurchaseInvoice"/> a <see cref="PurchaseInvoiceAudit"/>.
/// Único lugar que conoce ambos lados (evento de dominio ↔ entidad de auditoría).
/// </summary>
public sealed class PurchaseInvoiceAuditHandler :
    INotificationHandler<PurchaseInvoiceConfirmedEvent>,
    INotificationHandler<PurchaseInvoiceCancelledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public PurchaseInvoiceAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(PurchaseInvoiceConfirmedEvent e, CancellationToken ct) => _audit.RecordAsync(
        PurchaseInvoiceAudit.Create(
            _context.Actor, _context.CompanyId, e.InvoiceId, e.SupplierId,
            e.InvoiceNumber, e.GrandTotal, ((IAuditEvent)e).Action),
        ct);

    public Task Handle(PurchaseInvoiceCancelledEvent e, CancellationToken ct) => _audit.RecordAsync(
        PurchaseInvoiceAudit.Create(
            _context.Actor, _context.CompanyId, e.InvoiceId, e.SupplierId,
            e.InvoiceNumber, e.GrandTotal, ((IAuditEvent)e).Action, ((IAuditEvent)e).Reason),
        ct);
}
