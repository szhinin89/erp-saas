namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — dato crudo de un medio de pago para
/// <see cref="SupplierPayment.Create"/>. Sin Id propio todavía: <c>Create</c> genera el
/// <see cref="SupplierPaymentMethodLine"/> real y expone su posición (índice en la lista de entrada)
/// para que <see cref="SupplierPaymentAllocationInput"/> pueda referenciarlo.
/// </summary>
public sealed record SupplierPaymentMethodLineInput(
    Guid PaymentMethodId,
    Guid FinancialDestinationId,
    decimal Amount,
    string? ReferenceNumber = null,
    string? CheckNumber = null,
    DateOnly? CheckDate = null,
    string? Notes = null
);

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — dato crudo de una aplicación a cuota para
/// <see cref="SupplierPayment.Create"/>. Mismo criterio de índice que
/// <see cref="SupplierPaymentMethodLineInput"/>.
/// </summary>
public sealed record SupplierPaymentApplicationLineInput(
    Guid AccountsPayableInstallmentId,
    decimal AmountApplied
);

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — celda de la matriz medio↔cuota para
/// <see cref="SupplierPayment.Create"/>. <see cref="MethodLineIndex"/>/<see cref="ApplicationLineIndex"/>
/// son posiciones dentro de las listas de <see cref="SupplierPaymentMethodLineInput"/>/
/// <see cref="SupplierPaymentApplicationLineInput"/> pasadas a la misma llamada — <c>Create</c> los
/// resuelve a los Id reales generados para cada línea.
/// </summary>
public sealed record SupplierPaymentAllocationInput(
    int MethodLineIndex,
    int ApplicationLineIndex,
    decimal Amount
);
