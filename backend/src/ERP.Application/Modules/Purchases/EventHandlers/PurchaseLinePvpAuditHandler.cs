using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Purchases.EventHandlers;

/// <summary>
/// Traduce <see cref="PurchaseLinePvpUpdatedEvent"/> a <see cref="PurchaseLinePvpAudit"/> —
/// cubre tanto la edición manual en borrador como la actualización automática de
/// <c>Item.BaseSalePrice</c> al confirmar (distinguidas por <c>Action</c>).
/// </summary>
public sealed class PurchaseLinePvpAuditHandler : INotificationHandler<PurchaseLinePvpUpdatedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;

    public PurchaseLinePvpAuditHandler(IAuditService audit, IAuditContext context)
    {
        _audit = audit;
        _context = context;
    }

    public Task Handle(PurchaseLinePvpUpdatedEvent e, CancellationToken ct) =>
        _audit.RecordAsync(
            PurchaseLinePvpAudit.Create(
                _context.Actor,
                _context.CompanyId,
                e.InvoiceId,
                e.InvoiceNumber,
                e.ItemId,
                e.OldPvp,
                e.NewPvp,
                ((IAuditEvent)e).Action
            ),
            ct
        );
}
