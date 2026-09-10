namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// PURCHASE-RETURN-ACCOUNTING-NOT-GENERATED-01 — construcción del <see cref="PostingFact"/> del
/// hecho compuesto de §19.1bis, extraída de <see cref="PurchaseReturnAuthorizedPostingTranslator"/>
/// para que la remediación de devoluciones históricas (autorizadas antes de que existiera la
/// <c>PostingRule</c> de <c>Purchases/PurchaseReturn</c>) reconstruya el mismo hecho a partir de los
/// campos ya persistidos en <c>PurchaseReturn</c> — sin duplicar la regla de negocio
/// (CostVarianceDebit/CostVarianceCredit mutuamente excluyentes) en dos lugares.
/// </summary>
public static class PurchaseReturnPostingFactBuilder
{
    private const string SourceModuleName = "Purchases";
    private const string FactTypeName = "PurchaseReturn";

    public static PostingFact Build(
        Guid tenantId,
        Guid companyId,
        Guid purchaseReturnId,
        DateOnly entryDate,
        decimal authorizedVatTotal,
        decimal authorizedIceTotal,
        decimal authorizedIrbpnrTotal,
        decimal appliedToPayableAmount,
        decimal supplierCreditAmount,
        decimal costVarianceTotal,
        decimal historicalCostTotal
    )
    {
        // §19.1bis: CostVarianceTotal puede ser positivo, negativo o cero — nunca las dos líneas
        // condicionales a la vez (JournalFactory omite la que resuelve a 0).
        var costVarianceDebit = Math.Max(costVarianceTotal, 0m);
        var costVarianceCredit = Math.Max(-costVarianceTotal, 0m);

        return new PostingFact(
            tenantId,
            companyId,
            SourceModuleName,
            FactTypeName,
            purchaseReturnId,
            entryDate,
            Subtotal: 0m,
            TotalVat: authorizedVatTotal,
            TotalIce: authorizedIceTotal,
            TotalDiscount: 0m,
            GrandTotal: 0m,
            AppliedToPayableAmount: appliedToPayableAmount,
            SupplierCreditAmount: supplierCreditAmount,
            CostVarianceDebitAmount: costVarianceDebit,
            CostVarianceCreditAmount: costVarianceCredit,
            HistoricalCostTotal: historicalCostTotal,
            TotalIrbpnr: authorizedIrbpnrTotal
        );
    }
}
