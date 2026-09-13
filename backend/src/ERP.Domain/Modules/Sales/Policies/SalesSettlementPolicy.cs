namespace ERP.Domain.Modules.Sales.Policies;

/// <summary>
/// SALES-SETTLEMENT-CREDIT-01 — separa "cuánto dinero real entró" de "cuánto queda pendiente por
/// cobrar" en una venta. El método de pago "Crédito" (<c>PaymentMethod.IsCreditAllowed</c>) nunca
/// cuenta como dinero recibido: es un marcador de que ese monto queda pendiente, no un cobro real
/// (ver <see cref="SalesPaymentModality"/> para la señal equivalente a nivel de forma de pago).
/// Único punto de este cálculo — nunca repetido ad-hoc en un handler.
/// </summary>
public readonly record struct SalesSettlementResult(decimal Total, decimal CashApplied, decimal PendingBalance)
{
    /// <summary>true cuando el saldo pendiente es cero dentro de la tolerancia de redondeo del
    /// módulo (<see cref="SalesSettlementPolicy.Tolerance"/>) — no exige condición de pago,
    /// fecha de vencimiento ni cronograma; no genera CxC.</summary>
    public bool IsFullyCovered => PendingBalance <= SalesSettlementPolicy.Tolerance;
}

public static class SalesSettlementPolicy
{
    /// <summary>
    /// Tolerancia monetaria para considerar saldada una venta — misma magnitud que
    /// INVOICE_PAYMENT_TOLERANCE en frontend/src/modules/sales/constants/tolerances.ts (absorbe
    /// errores de redondeo cuando hay múltiples formas de pago con decimales). Cambiar aquí sin
    /// cambiar el equivalente de frontend puede producir mensajes inconsistentes entre ambas capas.
    /// </summary>
    public const decimal Tolerance = 0.02m;

    /// <summary>
    /// <paramref name="total"/>: GrandTotal de la venta. <paramref name="cashApplied"/>: suma de
    /// pagos aplicados EXCLUYENDO cualquier pago registrado con un método de pago
    /// <c>IsCreditAllowed = true</c> — ese monto nunca es dinero recibido para este cálculo.
    /// </summary>
    public static SalesSettlementResult Calculate(decimal total, decimal cashApplied)
    {
        var pending = total - cashApplied;
        if (pending < 0)
            pending = 0;
        return new SalesSettlementResult(total, cashApplied, pending);
    }
}
