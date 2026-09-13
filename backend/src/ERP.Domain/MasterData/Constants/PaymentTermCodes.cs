namespace ERP.Domain.MasterData.Constants;

/// <summary>
/// Código canónico de la condición de pago "Contado" sembrada por <c>SalesBootstrapStep</c> para
/// toda empresa nueva (Tipo A, <c>IsSystemSeeded = true</c>) — SSOT del código, evita duplicar el
/// literal "CONTADO" en Application/Infrastructure. Único uso permitido fuera del seed:
/// SALES-SETTLEMENT-CREDIT-01 — fallback de <c>PaymentTermSnapshot</c> cuando una venta queda
/// saldada por completo (saldo pendiente ≤ tolerancia) y no hay condición de pago explícita ni
/// default de cliente/empresa configurado. Nunca usar como "primer PaymentTerm activo del
/// catálogo" genérico — esa regla fue removida a propósito (ADR-033).
/// </summary>
public static class PaymentTermCodes
{
    public const string Cash = "CONTADO";
}
