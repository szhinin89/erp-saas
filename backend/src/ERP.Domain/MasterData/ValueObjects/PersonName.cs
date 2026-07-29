namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Nombre legal y comercial del BusinessPartner.
/// Almacenado aplanado en columnas de master_business_partners.
/// </summary>
public sealed record PersonName
{
    public const int LegalNameMaxLen = 200;
    public const int TradeNameMaxLen = 200;

    public string LegalName { get; }
    public string? TradeName { get; }

    private PersonName(string legalName, string? tradeName)
    {
        LegalName = legalName;
        TradeName = tradeName;
    }

    public static PersonName Create(string legalName, string? tradeName = null)
    {
        var legal = (legalName ?? string.Empty).Trim();
        if (legal.Length < 2)
            throw new ArgumentException("El nombre legal es obligatorio y debe tener al menos 2 caracteres.", nameof(legalName));
        if (legal.Length > LegalNameMaxLen)
            throw new ArgumentException($"El nombre legal no puede superar {LegalNameMaxLen} caracteres.", nameof(legalName));

        var trade = tradeName?.Trim();
        if (trade is { Length: 0 }) trade = null;
        if (trade?.Length > TradeNameMaxLen)
            throw new ArgumentException($"El nombre comercial no puede superar {TradeNameMaxLen} caracteres.", nameof(tradeName));

        return new PersonName(legal, trade);
    }

    public override string ToString() => TradeName is not null ? $"{LegalName} ({TradeName})" : LegalName;
}
