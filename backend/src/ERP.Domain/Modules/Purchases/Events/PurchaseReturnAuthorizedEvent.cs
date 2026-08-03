using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Devolución de compra autorizada (P0-02). Implementa <see cref="IAuditEvent"/> — listo para que
/// una fase posterior (Fase 6) lo audite (Entity Audit) y lo traduzca al hecho contable compuesto
/// balanceado de diseño §19.1bis, sin modificar este evento. Todos los montos ya están resueltos
/// por el dominio de <see cref="Entities.PurchaseReturn"/> — Accounting los consume tal cual,
/// nunca los recalcula.
/// </summary>
public sealed class PurchaseReturnAuthorizedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PurchaseReturnId { get; }
    public Guid PurchaseInvoiceId { get; }
    public Guid SupplierId { get; }
    public Guid BranchId { get; }
    public Guid CompanyId { get; }
    public string ReturnNumber { get; }
    public Guid UserId { get; }

    public decimal AuthorizedSubtotal { get; }
    public decimal AuthorizedVatTotal { get; }
    public decimal AuthorizedIceTotal { get; }
    public decimal AuthorizedDiscountTotal { get; }
    public decimal GrandTotal { get; }

    /// <summary>Costo histórico de inventario revertido — nunca el valor reconocido (§19.1bis).</summary>
    public decimal HistoricalCostTotal { get; }

    /// <summary>Puede ser positivo, negativo o cero — línea condicional del asiento compuesto (§19.1bis).</summary>
    public decimal CostVarianceTotal { get; }

    public decimal AppliedToPayableAmount { get; }
    public decimal SupplierCreditAmount { get; }

    /// <summary>Id del <see cref="Entities.SupplierCredit"/> creado — null si el excedente fue cero.</summary>
    public Guid? SupplierCreditId { get; }

    public string Reason { get; }

    Guid IAuditEvent.EntityId => PurchaseReturnId;
    string IAuditEvent.Action => "Authorized";
    string? IAuditEvent.Reason => Reason;

    public PurchaseReturnAuthorizedEvent(
        Guid purchaseReturnId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        Guid branchId,
        Guid tenantId,
        Guid companyId,
        string returnNumber,
        Guid userId,
        decimal authorizedSubtotal,
        decimal authorizedVatTotal,
        decimal authorizedIceTotal,
        decimal authorizedDiscountTotal,
        decimal grandTotal,
        decimal historicalCostTotal,
        decimal costVarianceTotal,
        decimal appliedToPayableAmount,
        decimal supplierCreditAmount,
        Guid? supplierCreditId,
        string reason
    )
    {
        PurchaseReturnId = purchaseReturnId;
        PurchaseInvoiceId = purchaseInvoiceId;
        SupplierId = supplierId;
        BranchId = branchId;
        TenantId = tenantId;
        CompanyId = companyId;
        ReturnNumber = returnNumber;
        UserId = userId;
        AuthorizedSubtotal = authorizedSubtotal;
        AuthorizedVatTotal = authorizedVatTotal;
        AuthorizedIceTotal = authorizedIceTotal;
        AuthorizedDiscountTotal = authorizedDiscountTotal;
        GrandTotal = grandTotal;
        HistoricalCostTotal = historicalCostTotal;
        CostVarianceTotal = costVarianceTotal;
        AppliedToPayableAmount = appliedToPayableAmount;
        SupplierCreditAmount = supplierCreditAmount;
        SupplierCreditId = supplierCreditId;
        Reason = reason;
    }
}
