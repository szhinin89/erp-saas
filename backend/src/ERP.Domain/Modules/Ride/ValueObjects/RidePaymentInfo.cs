namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>Una forma de pago declarada en el comprobante (<c>pago</c> en el XML autorizado).</summary>
public sealed record RidePaymentInfo
{
    public string PaymentMethodCode { get; }
    public decimal Amount { get; }
    public int? Term { get; }
    public string? TimeUnit { get; }

    private RidePaymentInfo(string paymentMethodCode, decimal amount, int? term, string? timeUnit)
    {
        PaymentMethodCode = paymentMethodCode;
        Amount = amount;
        Term = term;
        TimeUnit = timeUnit;
    }

    public static RidePaymentInfo Create(
        string paymentMethodCode,
        decimal amount,
        int? term = null,
        string? timeUnit = null
    )
    {
        if (string.IsNullOrWhiteSpace(paymentMethodCode))
            throw new ArgumentException(
                "El código de forma de pago es obligatorio.",
                nameof(paymentMethodCode)
            );
        if (amount < 0)
            throw new ArgumentException("El monto pagado no puede ser negativo.", nameof(amount));
        if (term is <= 0)
            throw new ArgumentException(
                "El plazo debe ser mayor a cero cuando se especifica.",
                nameof(term)
            );

        return new RidePaymentInfo(
            paymentMethodCode.Trim(),
            amount,
            term,
            string.IsNullOrWhiteSpace(timeUnit) ? null : timeUnit.Trim()
        );
    }
}
