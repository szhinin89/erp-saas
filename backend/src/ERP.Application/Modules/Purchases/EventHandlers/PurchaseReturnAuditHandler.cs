using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Purchases.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="Entities.PurchaseReturn"/> a
/// <see cref="PurchaseReturnAudit"/> (ADR-022, Entity Audit). P0-02 Fase 6 cubrió <c>Authorized</c>;
/// Fase 9 agregó <c>SupplierCreditNoteLinked</c>; Fase 10 completa con <c>Cancelled</c> (Open/Closed,
/// mismo criterio que <c>SalesReturnAuditHandler</c>/<c>PricingRuleAuditHandler</c>).
/// </summary>
public sealed class PurchaseReturnAuditHandler
    : INotificationHandler<PurchaseReturnAuthorizedEvent>,
        INotificationHandler<PurchaseReturnSupplierCreditNoteLinkedEvent>,
        INotificationHandler<PurchaseReturnCancelledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public PurchaseReturnAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(PurchaseReturnAuthorizedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PurchaseReturnAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.BranchId,
                e.PurchaseReturnId,
                e.PurchaseInvoiceId,
                ((IAuditEvent)e).Action,
                supplierId: e.SupplierId,
                returnNumber: e.ReturnNumber,
                grandTotal: e.GrandTotal,
                reason: e.Reason
            ),
            ct
        );

    public Task Handle(PurchaseReturnSupplierCreditNoteLinkedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PurchaseReturnAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.BranchId,
                e.PurchaseReturnId,
                e.PurchaseInvoiceId,
                ((IAuditEvent)e).Action,
                supplierId: e.SupplierId,
                returnNumber: e.ReturnNumber,
                grandTotal: e.GrandTotal,
                reason: ((IAuditEvent)e).Reason
            ),
            ct
        );

    public Task Handle(PurchaseReturnCancelledEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PurchaseReturnAudit.Create(
                _context.Actor,
                e.CompanyId,
                e.BranchId,
                e.PurchaseReturnId,
                e.PurchaseInvoiceId,
                ((IAuditEvent)e).Action,
                supplierId: e.SupplierId,
                returnNumber: e.ReturnNumber,
                grandTotal: e.AppliedToPayableAmount is not null
                    ? e.AppliedToPayableAmount + e.SupplierCreditAmount
                    : null,
                reason: ((IAuditEvent)e).Reason
            ),
            ct
        );
}
