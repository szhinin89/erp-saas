using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="IssuedWithholding"/>: cubre Issued/Cancelled.
/// Append-only — nunca se edita ni se borra.
/// </summary>
public sealed class IssuedWithholdingAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PurchaseInvoiceId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string WithholdingNumber { get; private set; } = null!;
    public decimal TotalRetained { get; private set; }

    private IssuedWithholdingAudit() { }

    public static IssuedWithholdingAudit Create(
        AuditActor actor, Guid companyId, Guid withholdingId, Guid purchaseInvoiceId, Guid supplierId,
        string withholdingNumber, decimal totalRetained, string action, string? reason = null)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new IssuedWithholdingAudit
        {
            CompanyId = companyId,
            PurchaseInvoiceId = purchaseInvoiceId,
            SupplierId = supplierId,
            WithholdingNumber = withholdingNumber,
            TotalRetained = totalRetained,
        };
        audit.SetCommon(actor, withholdingId, action, reason);
        return audit;
    }
}
