using System.Text.Json;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — datos económicos del hecho de origen persistidos en la obligación
/// (<c>AccountingPosting.Payload</c>, versión <see cref="CurrentVersion"/>). Solo importes y
/// referencias de origen ya resueltos por el módulo operativo; nunca cuentas contables: las
/// cuentas se resuelven con la configuración vigente en cada intento (accounting.md §6).
/// </summary>
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
    IReadOnlyList<AccountingPostingExpenseLine>? ExpenseLines = null
)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    public static AccountingPostingPayload? Deserialize(int version, string payload) =>
        version == CurrentVersion
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
