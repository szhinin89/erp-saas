using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Auditoría de dominio del PVP snapshot de línea de compra — cubre tanto la edición manual en
/// borrador (action "Updated") como la actualización automática de <c>Item.BaseSalePrice</c> al
/// confirmar (action "ConfirmedUpdate"). <see cref="AuditRecordBase.EntityId"/> es el
/// <see cref="ItemId"/> afectado en ambos casos. Append-only — nunca se edita ni se borra.
/// </summary>
public sealed class PurchaseLinePvpAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PurchaseInvoiceId { get; private set; }
    public string InvoiceNumber { get; private set; } = null!;
    public Guid ItemId { get; private set; }
    public decimal OldPvp { get; private set; }
    public decimal NewPvp { get; private set; }

    private PurchaseLinePvpAudit() { }

    public static PurchaseLinePvpAudit Create(
        AuditActor actor, Guid companyId, Guid purchaseInvoiceId, string invoiceNumber,
        Guid itemId, decimal oldPvp, decimal newPvp, string action, string? reason = null)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new PurchaseLinePvpAudit
        {
            CompanyId = companyId,
            PurchaseInvoiceId = purchaseInvoiceId,
            InvoiceNumber = invoiceNumber,
            ItemId = itemId,
            OldPvp = oldPvp,
            NewPvp = newPvp,
        };
        audit.SetCommon(actor, itemId, action, reason);
        return audit;
    }
}
