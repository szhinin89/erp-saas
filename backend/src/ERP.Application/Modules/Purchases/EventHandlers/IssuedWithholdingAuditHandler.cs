using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Purchases.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="IssuedWithholding"/> a <see cref="IssuedWithholdingAudit"/>.
/// </summary>
public sealed class IssuedWithholdingAuditHandler
    : INotificationHandler<IssuedWithholdingIssuedEvent>,
        INotificationHandler<IssuedWithholdingCancelledEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public IssuedWithholdingAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(IssuedWithholdingIssuedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            IssuedWithholdingAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.WithholdingId,
                e.PurchaseInvoiceId,
                e.SupplierId,
                e.WithholdingNumber,
                e.TotalRetained,
                ((IAuditEvent)e).Action
            ),
            ct
        );

    public Task Handle(IssuedWithholdingCancelledEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            IssuedWithholdingAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.WithholdingId,
                e.PurchaseInvoiceId,
                e.SupplierId,
                e.WithholdingNumber,
                e.TotalRetained,
                ((IAuditEvent)e).Action,
                ((IAuditEvent)e).Reason
            ),
            ct
        );
}
