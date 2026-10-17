using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="PurchaseReturnAuthorizedEvent"/> al hecho compuesto Purchases/PurchaseReturn
/// (§19.1bis) y lo registra como obligación durable en la transacción de la autorización
/// (ACCOUNTING-CONTINUITY-04). Único hecho por devolución: la NC de compra tipo Return vinculada
/// nunca genera otro asiento.
/// </summary>
public sealed class PurchaseReturnAuthorizedPostingTranslator(
    AccountingPostingRecorder recorder,
    ICompanyClock companyClock
) : INotificationHandler<PurchaseReturnAuthorizedEvent>
{
    public async Task Handle(PurchaseReturnAuthorizedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var entryDate = await companyClock.TodayAsync(e.CompanyId, tenantId, ct);
        await recorder.RecordAsync(
            PurchaseReturnPosting.Request(
                tenantId,
                e.CompanyId,
                e.PurchaseReturnId,
                e.ReturnNumber,
                entryDate,
                e.AuthorizedVatTotal,
                e.AuthorizedIceTotal,
                e.AuthorizedIrbpnrTotal,
                e.AppliedToPayableAmount,
                e.SupplierCreditAmount,
                e.CostVarianceTotal,
                e.HistoricalCostTotal,
                e.PurchaseInvoiceId,
                e.SupplierCreditId
            ),
            ct
        );
    }
}

/// <summary>
/// Hecho compuesto de la devolución de compra, compartido por la autorización y por la remediación
/// manual por CLI: una sola definición de sus componentes (IVA/ICE/IRBPNR devueltos, CxP aplicada,
/// saldo a favor, costo histórico retirado y diferencia de costo Debe/Haber mutuamente excluyentes).
/// </summary>
public static class PurchaseReturnPosting
{
    public const string SourceModuleName = "Purchases";
    public const string FactTypeName = "PurchaseReturn";

    public static AccountingPostingRequest Request(
        Guid tenantId,
        Guid companyId,
        Guid purchaseReturnId,
        string? returnNumber,
        DateOnly entryDate,
        decimal authorizedVatTotal,
        decimal authorizedIceTotal,
        decimal authorizedIrbpnrTotal,
        decimal appliedToPayableAmount,
        decimal supplierCreditAmount,
        decimal costVarianceTotal,
        decimal historicalCostTotal,
        Guid purchaseInvoiceId,
        Guid? supplierCreditId
    )
    {
        var references = new Dictionary<string, Guid> { ["PurchaseInvoiceId"] = purchaseInvoiceId };
        if (supplierCreditId is { } creditId)
            references["SupplierCreditId"] = creditId;

        return new AccountingPostingRequest(
            tenantId,
            companyId,
            SourceModuleName,
            FactTypeName,
            purchaseReturnId,
            returnNumber,
            entryDate,
            entryDate,
            new AccountingPostingPayload(
                0m,
                authorizedVatTotal,
                authorizedIceTotal,
                0m,
                0m,
                TotalIrbpnr: authorizedIrbpnrTotal,
                AppliedToPayableAmount: appliedToPayableAmount,
                SupplierCreditAmount: supplierCreditAmount,
                CostVarianceDebitAmount: Math.Max(costVarianceTotal, 0m),
                CostVarianceCreditAmount: Math.Max(-costVarianceTotal, 0m),
                HistoricalCostTotal: historicalCostTotal,
                References: references
            )
        );
    }
}
