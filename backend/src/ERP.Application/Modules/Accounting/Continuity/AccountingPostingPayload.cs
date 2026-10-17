using System.Text.Json;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — datos económicos del hecho de origen persistidos en la obligación
/// (<c>AccountingPosting.Payload</c>, versión <see cref="CurrentVersion"/>). Solo importes y
/// referencias de origen ya resueltos por el módulo operativo; nunca cuentas contables: las
/// cuentas se resuelven con la configuración vigente en cada intento (accounting.md §6).
/// Excepción: Reversal conserva OriginalJournalEntryId y motivo; sus líneas vienen exclusivamente
/// del libro histórico, nunca de cuentas o reglas vigentes.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-04 (versión 2) — agrega los componentes de los hechos compuestos
/// (CxP aplicada, anticipo/crédito de proveedor, diferencia de costo, costo histórico, contado y
/// saldo CxC), los destinos financieros por identidad (caja, cuenta bancaria, medio de pago) y
/// referencias de origen. Todos opcionales: un payload versión 1 se lee sin cambios.
/// </remarks>
public sealed record AccountingPostingPayload(
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalIce,
    decimal TotalDiscount,
    decimal GrandTotal,
    decimal? TotalIrbpnr = null,
    decimal? RetainedAmount = null,
    decimal? RetainedVatAmount = null,
    decimal? RetainedIncomeAmount = null,
    IReadOnlyList<AccountingPostingExpenseLine>? ExpenseLines = null,
    Guid? OriginalJournalEntryId = null,
    string? ReversalReason = null,
    decimal? AppliedToPayableAmount = null,
    decimal? SupplierCreditAmount = null,
    decimal? CostVarianceDebitAmount = null,
    decimal? CostVarianceCreditAmount = null,
    decimal? HistoricalCostTotal = null,
    decimal? CashApplied = null,
    decimal? PendingBalance = null,
    IReadOnlyList<AccountingPostingDestination>? Destinations = null,
    IReadOnlyDictionary<string, Guid>? References = null
)
{
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    public static AccountingPostingPayload? Deserialize(int version, string payload) =>
        version is 1 or CurrentVersion
            ? JsonSerializer.Deserialize<AccountingPostingPayload>(payload, Options)
            : null;
}

/// <summary>
/// Línea de gasto: la subcategoría es el dato de origen; su cuenta contable se resuelve en cada
/// intento con la configuración vigente.
/// </summary>
public sealed record AccountingPostingExpenseLine(
    Guid ExpenseLineId,
    Guid ExpenseSubcategoryId,
    decimal Amount,
    string? Description
);

/// <summary>
/// ACCOUNTING-CONTINUITY-04 — destino financiero real del hecho, por identidad operativa (nunca
/// por cuenta contable): su cuenta se resuelve en cada intento con la configuración vigente.
/// Sin <see cref="OverridesAmountKind"/> produce una línea propia (allocation); con él sustituye
/// la cuenta de la línea de la regla de ese componente y naturaleza.
/// </summary>
public sealed record AccountingPostingDestination(
    string Kind,
    Guid ReferenceId,
    decimal Amount,
    AccountNature Nature,
    PostingAmountKind? OverridesAmountKind = null,
    string? Description = null
)
{
    public const string CashRegister = "CashRegister";
    public const string BankAccount = "BankAccount";
    public const string PaymentMethod = "PaymentMethod";

    /// <summary>Destino de un medio con caja o cuenta bancaria (exactamente uno).</summary>
    public static AccountingPostingDestination FromCashOrBank(
        Guid? companyBankAccountId,
        Guid? cashRegisterId,
        decimal amount,
        AccountNature nature,
        PostingAmountKind? overridesAmountKind = null,
        string? description = null
    ) =>
        (companyBankAccountId, cashRegisterId) switch
        {
            ({ } bank, null) => new(BankAccount, bank, amount, nature, overridesAmountKind, description),
            (null, { } cash) => new(CashRegister, cash, amount, nature, overridesAmountKind, description),
            _ => throw new ERP.Domain.Exceptions.DomainRuleViolationException(
                "El medio debe tener exactamente un destino financiero: cuenta bancaria o caja."
            ),
        };
}
