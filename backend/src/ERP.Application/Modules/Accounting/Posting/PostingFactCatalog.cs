using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.InitialLoad.Constants;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// ACCOUNTING-11 — lado de un asiento que el módulo de origen completa con
/// <see cref="PostingFact.Allocations"/> (cardinalidad variable: cajas, bancos, medios de pago,
/// categorías de gasto). En ese lado la regla no necesita una línea fija.
/// </summary>
[Flags]
public enum PostingDynamicSides
{
    None = 0,
    Debit = 1,
    Credit = 2,
    Both = Debit | Credit,
}

/// <summary>
/// ACCOUNTING-11 — hecho contable soportado por el Posting Engine: clave (SourceModule, FactType),
/// montos que su traductor aporta (<see cref="AllowedAmountKinds"/>) y lados dinámicos.
/// Sin <see cref="AllowedAmountKinds"/> la regla es habilitadora: no admite líneas fijas.
/// </summary>
public sealed record PostingFactDefinition(
    string SourceModule,
    string FactType,
    IReadOnlyList<PostingAmountKind> AllowedAmountKinds,
    PostingDynamicSides DynamicSides = PostingDynamicSides.None
)
{
    public bool AllowsFixedLines => AllowedAmountKinds.Count > 0;
}

/// <summary>
/// ACCOUNTING-11 — autoridad runtime única de los hechos contables que admiten PostingRule
/// (accounting.md §2). Alimenta la validación de Create/Update, <c>GET posting-rules/catalog</c> y el
/// bootstrap de reglas iniciales. Un hecho que no figura aquí no tiene traductor que resuelva
/// regla (p. ej. anulaciones que espejan el asiento histórico, accounting.md §7). Los AmountKinds
/// permitidos son exactamente los montos que el traductor del hecho transporta en
/// <see cref="PostingFact"/>; ampliar uno exige que su traductor aporte el monto.
/// </summary>
public static class PostingFactCatalog
{
    private static readonly PostingAmountKind[] SaleKinds =
    [
        PostingAmountKind.CashApplied,
        PostingAmountKind.PendingBalance,
        PostingAmountKind.Discount,
        PostingAmountKind.Subtotal,
        PostingAmountKind.TaxVat,
        PostingAmountKind.TaxIce,
        PostingAmountKind.TaxIrbpnr,
    ];

    private static readonly PostingAmountKind[] PayableSplitKinds =
    [
        PostingAmountKind.AppliedToPayable,
        PostingAmountKind.SupplierCredit,
    ];

    public static readonly PostingFactDefinition SalesInvoiceIssued = new("Sales", "InvoiceIssued", SaleKinds);
    public static readonly PostingFactDefinition SalesReturn = new("Sales", "SalesReturn", SaleKinds);
    public static readonly PostingFactDefinition CostOfGoodsSold = new(
        "Sales",
        "CostOfGoodsSold",
        [PostingAmountKind.HistoricalCost]
    );
    public static readonly PostingFactDefinition CostOfGoodsSoldReversed = new(
        "Sales",
        "CostOfGoodsSoldReversed",
        [PostingAmountKind.HistoricalCost]
    );
    public static readonly PostingFactDefinition PurchaseInvoiceReceived = new(
        "Purchases",
        "InvoiceReceived",
        [
            PostingAmountKind.Subtotal,
            PostingAmountKind.TaxVat,
            PostingAmountKind.TaxIce,
            PostingAmountKind.TaxIrbpnr,
            PostingAmountKind.GrandTotal,
        ]
    );
    public static readonly PostingFactDefinition PurchaseCreditNoteAuthorized = new(
        "Purchases",
        "PurchaseCreditNoteAuthorized",
        [
            PostingAmountKind.AppliedToPayable,
            PostingAmountKind.Subtotal,
            PostingAmountKind.TaxIce,
            PostingAmountKind.TaxVat,
        ]
    );
    public static readonly PostingFactDefinition PurchaseReturn = new(
        "Purchases",
        "PurchaseReturn",
        [
            PostingAmountKind.AppliedToPayable,
            PostingAmountKind.SupplierCredit,
            PostingAmountKind.CostVarianceDebit,
            PostingAmountKind.HistoricalCost,
            PostingAmountKind.TaxVat,
            PostingAmountKind.TaxIce,
            PostingAmountKind.TaxIrbpnr,
            PostingAmountKind.CostVarianceCredit,
        ]
    );
    public static readonly PostingFactDefinition SupplierCreditApplied = new(
        "Purchases",
        "SupplierCreditApplied",
        [PostingAmountKind.GrandTotal]
    );
    public static readonly PostingFactDefinition SupplierCreditRefunded = new(
        "Purchases",
        "SupplierCreditRefunded",
        [PostingAmountKind.GrandTotal],
        PostingDynamicSides.Debit
    );
    public static readonly PostingFactDefinition CollectionApplied = new(
        "Finance",
        "CollectionApplied",
        [PostingAmountKind.GrandTotal]
    );
    public static readonly PostingFactDefinition SupplierPaymentConfirmed = new(
        "Payables",
        "SupplierPaymentConfirmed",
        PayableSplitKinds,
        PostingDynamicSides.Credit
    );
    public static readonly PostingFactDefinition SupplierPaymentReversed = new(
        "Payables",
        "SupplierPaymentReversed",
        PayableSplitKinds,
        PostingDynamicSides.Debit
    );
    public static readonly PostingFactDefinition RetentionDocumentIssued = new(
        "Retentions",
        "DocumentIssued",
        [
            PostingAmountKind.Retention,
            PostingAmountKind.RetentionVat,
            PostingAmountKind.RetentionIncome,
        ]
    );
    public static readonly PostingFactDefinition ExpenseDocumentConfirmed = new(
        "Expenses",
        "DocumentConfirmed",
        [PostingAmountKind.TaxVat, PostingAmountKind.GrandTotal],
        PostingDynamicSides.Debit
    );
    /// <summary>
    /// ACCOUNTING-12A — ajustes de inventario: la regla fija Inventario por el TotalCost del Kardex
    /// (GrandTotal) y el origen aporta la contrapartida con la cuenta del motivo (lado dinámico).
    /// </summary>
    public static readonly PostingFactDefinition StockAdjustmentIn = new(
        "Inventory",
        "StockAdjustmentIn",
        [PostingAmountKind.GrandTotal],
        PostingDynamicSides.Credit
    );
    public static readonly PostingFactDefinition StockAdjustmentOut = new(
        "Inventory",
        "StockAdjustmentOut",
        [PostingAmountKind.GrandTotal],
        PostingDynamicSides.Debit
    );
    /// <summary>
    /// ACCOUNTING-12C — movimientos manuales de Caja y depósito bancario: reglas habilitadoras. Ambos
    /// lados los aporta el origen (la cuenta de la caja real de la sesión y la del motivo o la del
    /// banco destino); no hay cuenta fija que la regla pueda fijar.
    /// </summary>
    public static readonly PostingFactDefinition CashManualIncome = new(
        "Caja",
        "ManualIncome",
        [],
        PostingDynamicSides.Both
    );
    public static readonly PostingFactDefinition CashManualExpense = new(
        "Caja",
        "ManualExpense",
        [],
        PostingDynamicSides.Both
    );
    public static readonly PostingFactDefinition CashWithdrawal = new(
        "Caja",
        "Withdrawal",
        [],
        PostingDynamicSides.Both
    );
    public static readonly PostingFactDefinition CashBankDeposit = new(
        "Caja",
        "BankDeposit",
        [],
        PostingDynamicSides.Both
    );
    /// <summary>
    /// ACCOUNTING-12C — diferencia de arqueo al cerrar la sesión: la regla fija la cuenta de
    /// diferencias (GrandTotal = |Difference|) y el origen aporta la caja real (lado dinámico).
    /// </summary>
    public static readonly PostingFactDefinition CashCountSurplus = new(
        "Caja",
        "CashCountSurplus",
        [PostingAmountKind.GrandTotal],
        PostingDynamicSides.Debit
    );
    public static readonly PostingFactDefinition CashCountShortage = new(
        "Caja",
        "CashCountShortage",
        [PostingAmountKind.GrandTotal],
        PostingDynamicSides.Credit
    );
    public static readonly PostingFactDefinition OpeningInventory = new(
        OpeningBalancePostingFacts.SourceModule,
        OpeningBalancePostingFacts.OpeningInventory,
        [PostingAmountKind.GrandTotal]
    );
    public static readonly PostingFactDefinition OpeningReceivables = new(
        OpeningBalancePostingFacts.SourceModule,
        OpeningBalancePostingFacts.OpeningReceivables,
        [PostingAmountKind.GrandTotal]
    );
    public static readonly PostingFactDefinition OpeningPayables = new(
        OpeningBalancePostingFacts.SourceModule,
        OpeningBalancePostingFacts.OpeningPayables,
        [PostingAmountKind.GrandTotal]
    );
    public static readonly PostingFactDefinition OpeningJournalEntry = new(
        OpeningBalancePostingFacts.SourceModule,
        OpeningBalancePostingFacts.OpeningJournalEntry,
        [],
        PostingDynamicSides.Both
    );

    public static readonly IReadOnlyList<PostingFactDefinition> All =
    [
        SalesInvoiceIssued,
        SalesReturn,
        CostOfGoodsSold,
        CostOfGoodsSoldReversed,
        PurchaseInvoiceReceived,
        PurchaseCreditNoteAuthorized,
        PurchaseReturn,
        SupplierCreditApplied,
        SupplierCreditRefunded,
        CollectionApplied,
        SupplierPaymentConfirmed,
        SupplierPaymentReversed,
        RetentionDocumentIssued,
        ExpenseDocumentConfirmed,
        StockAdjustmentIn,
        StockAdjustmentOut,
        CashManualIncome,
        CashManualExpense,
        CashWithdrawal,
        CashBankDeposit,
        CashCountSurplus,
        CashCountShortage,
        OpeningInventory,
        OpeningReceivables,
        OpeningPayables,
        OpeningJournalEntry,
    ];

    public static PostingFactDefinition? Find(string sourceModule, string factType) =>
        All.FirstOrDefault(d => d.SourceModule == sourceModule && d.FactType == factType);

    /// <summary>
    /// Forma de las líneas fijas de una regla para el hecho dado (sin cuentas: la validación de
    /// cuentas es de Application). Devuelve el primer error o <c>null</c>.
    /// </summary>
    public static string? ValidateLineShape(
        PostingFactDefinition fact,
        IReadOnlyCollection<(AccountNature Nature, PostingAmountKind AmountKind)> lines
    )
    {
        if (!fact.AllowsFixedLines)
            return lines.Count == 0
                ? null
                : "Esta regla es habilitadora: todas sus líneas las aporta el origen y no admite líneas fijas.";

        var notAllowed = lines.Where(l => !fact.AllowedAmountKinds.Contains(l.AmountKind)).ToList();
        if (notAllowed.Count > 0)
            return $"El tipo de monto '{notAllowed[0].AmountKind}' no está disponible para {fact.SourceModule}/{fact.FactType}.";

        if (lines.GroupBy(l => l).Any(g => g.Count() > 1))
            return "Hay líneas repetidas con la misma dirección y tipo de monto: contabilizarían el importe dos veces.";

        var hasDynamic = fact.DynamicSides != PostingDynamicSides.None;
        if (!hasDynamic && lines.Count < 2)
            return "La regla necesita al menos 2 líneas.";
        if (lines.Count == 0)
            return "La regla necesita al menos 1 línea.";
        if (!fact.DynamicSides.HasFlag(PostingDynamicSides.Debit) && lines.All(l => l.Nature != AccountNature.Debit))
            return "La regla necesita al menos una línea al Debe.";
        if (!fact.DynamicSides.HasFlag(PostingDynamicSides.Credit) && lines.All(l => l.Nature != AccountNature.Credit))
            return "La regla necesita al menos una línea al Haber.";
        return null;
    }
}
