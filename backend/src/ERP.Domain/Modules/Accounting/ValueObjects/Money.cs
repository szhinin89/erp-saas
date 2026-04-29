namespace ERP.Domain.Accounting.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("La moneda debe ser un codigo ISO de 3 letras.");
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("No se pueden sumar monedas distintas.");
        return new(Amount + other.Amount, Currency);
    }

    public Money Negate() => new(-Amount, Currency);

    public override string ToString() => $"{Amount:F2} {Currency}";
}
