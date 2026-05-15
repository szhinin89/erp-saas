namespace ERP.Domain.Modules.Accounting.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("El monto no puede ser negativo.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new ArgumentException("La moneda debe ser un código ISO de 3 letras (ej: USD, EUR).");

        Amount   = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException(
                $"No se pueden sumar monedas distintas: {Currency} y {other.Currency}.");

        return new(Amount + other.Amount, Currency);
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
