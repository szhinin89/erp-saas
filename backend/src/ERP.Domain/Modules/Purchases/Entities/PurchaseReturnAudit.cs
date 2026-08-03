using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="PurchaseReturn"/> (ADR-022, Entity Audit) — cubre las
/// transiciones Created/Authorized/Cancelled/NC vinculada. <see cref="GrandTotal"/> solo se
/// conoce al autorizar (los totales se congelan recién en <c>PurchaseReturn.Authorize()</c>) —
/// queda <c>null</c> en el resto de acciones. Append-only — nunca se edita ni se borra.
/// </summary>
public sealed class PurchaseReturnAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// <c>BranchId</c> del agregado auditado (diseño §20.1, párrafo posterior a la tabla) —
    /// mismo valor persistido y congelado de <see cref="PurchaseReturn.BranchId"/> en el momento
    /// del evento (Branch Ownership Rule, §5.2). Histórico, nunca resuelto desde la sucursal
    /// activa del operador en tiempo de ejecución; no es una columna de identidad del actor (esa
    /// vive exclusivamente en <see cref="AuditActor"/>).
    /// </summary>
    public Guid BranchId { get; private set; }

    public Guid PurchaseInvoiceId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string? ReturnNumber { get; private set; }
    public decimal? GrandTotal { get; private set; }

    private PurchaseReturnAudit() { }

    public static PurchaseReturnAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid branchId,
        Guid purchaseReturnId,
        Guid purchaseInvoiceId,
        string action,
        Guid? supplierId = null,
        string? returnNumber = null,
        decimal? grandTotal = null,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("branchId requerido.", nameof(branchId));

        var audit = new PurchaseReturnAudit
        {
            CompanyId = companyId,
            BranchId = branchId,
            PurchaseInvoiceId = purchaseInvoiceId,
            SupplierId = supplierId,
            ReturnNumber = returnNumber,
            GrandTotal = grandTotal,
        };
        audit.SetCommon(actor, purchaseReturnId, action, reason);
        return audit;
    }
}
