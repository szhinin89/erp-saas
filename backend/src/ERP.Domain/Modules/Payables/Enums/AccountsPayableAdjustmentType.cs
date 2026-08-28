namespace ERP.Domain.Modules.Payables.Enums;

/// <summary>
/// PAYABLES-PURCHASE-MIGRATION-10 — categoría de un ajuste aplicado contra una
/// <see cref="Entities.AccountsPayableInstallment"/>. Cada categoría es un track independiente
/// (mismo criterio que <c>PurchasePayable</c> original: pago, devolución, crédito de proveedor,
/// nota de crédito y retención nunca se mezclan en un único acumulador, para trazabilidad
/// diferenciada) — la única diferencia es que aquí el acumulador vive en la cuota, no en la
/// cabecera (<see cref="Entities.AccountsPayable"/> los deriva sumando sus cuotas).
/// </summary>
public enum AccountsPayableAdjustmentType
{
    Payment,
    Retention,
    ReturnCredit,
    SupplierCredit,
    CreditNote,
}
