namespace ERP.Domain.Modules.Sales.Policies;

/// <summary>
/// Modalidad de pago real de una venta (BUGFIX-SALES-CREDIT-PAYMENT-CONSISTENCY-01), derivada de
/// dos señales independientes: la condición de pago (<c>PaymentTerm</c>) y el método de pago
/// realmente usado (<c>PaymentMethod.IsCreditAllowed</c>). Único punto de esta derivación — nunca
/// recalcular "es crédito" por separado en cada validación (ver el bug real de la factura
/// 001-001-000000016: PaymentTerm Contado pagada con método Crédito, sin generar CxC).
/// </summary>
public readonly record struct SalesPaymentModality(bool IsCreditByTerm, bool IsCreditByPaymentMethod)
{
    /// <summary>Cualquier señal de crédito (término o método) marca la venta como crédito.</summary>
    public bool IsCreditSale => IsCreditByTerm || IsCreditByPaymentMethod;

    public bool IsCashSale => !IsCreditSale;

    /// <summary>
    /// Término y método deben "estar de acuerdo": ambos contado o ambos crédito. Venta mixta
    /// (parte contado + parte crédito) queda fuera de alcance de esta fase — ver
    /// BUGFIX-SALES-CREDIT-PAYMENT-CONSISTENCY-01.
    /// </summary>
    public bool IsConsistent => IsCreditByTerm == IsCreditByPaymentMethod;
}

/// <summary>
/// Mensajes de la regla de consistencia condición de pago ↔ método de pago. Independiente de
/// <see cref="SalesFiscalPolicyMessages"/> (Consumidor Final) — esta regla aplica a cualquier
/// cliente; el mensaje fiscal de Consumidor Final tiene prioridad cuando ambas aplican (ver
/// AuthorizeSalesInvoiceHandler: la validación de Consumidor Final se evalúa primero).
/// </summary>
public static class SalesPaymentConsistencyMessages
{
    public const string TermCashMethodCreditMessage =
        "La condición de pago es contado, pero se seleccionó un método de pago de crédito. Use una condición de pago a crédito o cambie el método de pago.";

    public const string TermCreditMethodCashOnlyMessage =
        "La condición de pago es crédito. Debe registrar el pago con método Crédito para generar la cuenta por cobrar.";
}
