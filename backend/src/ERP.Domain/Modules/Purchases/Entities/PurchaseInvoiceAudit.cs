using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="PurchaseInvoice"/>: cubre las transiciones de estado
/// Confirmed/Cancelled. Append-only — nunca se edita ni se borra.
/// </summary>
public sealed class PurchaseInvoiceAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string InvoiceNumber { get; private set; } = null!;
    public decimal GrandTotal { get; private set; }

    private PurchaseInvoiceAudit() { }

    public static PurchaseInvoiceAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid invoiceId,
        Guid supplierId,
        string invoiceNumber,
        decimal grandTotal,
        string action,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new PurchaseInvoiceAudit
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            InvoiceNumber = invoiceNumber,
            GrandTotal = grandTotal,
        };
        audit.SetCommon(actor, invoiceId, action, reason);
        return audit;
    }
}
