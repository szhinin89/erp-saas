using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Se levanta cuando <c>PurchaseReturn.Cancel()</c> anula una devolución — desde <c>Draft</c> (sin
/// montos congelados, todos los snapshots quedan <c>null</c>) o desde <c>Authorized</c> (con los
/// montos ya congelados en <c>Authorize()</c>, necesarios para que una fase posterior reverse
/// exactamente el mismo asiento contable y el mismo crédito aplicado a la CxP — nunca recalculados,
/// diseño §19.1bis "reversa exactamente los mismos montos snapshot").
/// </summary>
public sealed class PurchaseReturnCancelledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PurchaseReturnId { get; }
    public Guid PurchaseInvoiceId { get; }
    public Guid SupplierId { get; }
    public Guid BranchId { get; }
    public Guid CompanyId { get; }
    public string? ReturnNumber { get; }
    public string Reason { get; }
    public Guid UserId { get; }

    /// <summary>Solo tiene valor si se cancela desde <c>Authorized</c> — null si se cancela desde <c>Draft</c>.</summary>
    public decimal? AppliedToPayableAmount { get; }
    public decimal? SupplierCreditAmount { get; }
    public decimal? HistoricalCostTotal { get; }
    public decimal? CostVarianceTotal { get; }
    public decimal? AuthorizedVatTotal { get; }
    public decimal? AuthorizedIceTotal { get; }

    /// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — solo tiene valor si se cancela desde <c>Authorized</c>, mismo criterio que <see cref="AuthorizedVatTotal"/>/<see cref="AuthorizedIceTotal"/>.</summary>
    public decimal? AuthorizedIrbpnrTotal { get; }

    Guid IAuditEvent.EntityId => PurchaseReturnId;
    string IAuditEvent.Action => "Cancelled";
    string? IAuditEvent.Reason => Reason;

    public PurchaseReturnCancelledEvent(
        Guid purchaseReturnId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        Guid branchId,
        Guid tenantId,
        Guid companyId,
        string? returnNumber,
        string reason,
        Guid userId,
        decimal? appliedToPayableAmount,
        decimal? supplierCreditAmount,
        decimal? historicalCostTotal,
        decimal? costVarianceTotal,
        decimal? authorizedVatTotal,
        decimal? authorizedIceTotal,
        decimal? authorizedIrbpnrTotal = null
    )
    {
        PurchaseReturnId = purchaseReturnId;
        PurchaseInvoiceId = purchaseInvoiceId;
        SupplierId = supplierId;
        BranchId = branchId;
        TenantId = tenantId;
        CompanyId = companyId;
        ReturnNumber = returnNumber;
        Reason = reason;
        UserId = userId;
        AppliedToPayableAmount = appliedToPayableAmount;
        SupplierCreditAmount = supplierCreditAmount;
        HistoricalCostTotal = historicalCostTotal;
        CostVarianceTotal = costVarianceTotal;
        AuthorizedVatTotal = authorizedVatTotal;
        AuthorizedIceTotal = authorizedIceTotal;
        AuthorizedIrbpnrTotal = authorizedIrbpnrTotal;
    }
}
