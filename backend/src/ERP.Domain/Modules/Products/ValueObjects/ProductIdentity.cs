namespace ERP.Domain.Products.ValueObjects;

/// <summary>
/// Value object para encapsular la identidad comercial de un producto.
/// Centraliza normalización y reglas de nombre/código.
/// </summary>
public sealed record ProductIdentity
{
    public string SaleCode { get; }
    public string ShortName { get; }
    public string Description { get; }

    private ProductIdentity(string saleCode, string shortName, string description)
    {
        SaleCode = saleCode;
        ShortName = shortName;
        Description = description;
    }

    public static ProductIdentity Create(string saleCode, string shortName, string description)
    {
        var normalizedSaleCode = NormalizeRequired(saleCode, nameof(saleCode), 50);
        var normalizedShortName = NormalizeRequired(shortName, nameof(shortName), 50);
        var normalizedDescription = NormalizeRequired(description, nameof(description), 254);

        return new ProductIdentity(normalizedSaleCode, normalizedShortName, normalizedDescription);
    }

    private static string NormalizeRequired(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El valor es obligatorio.", paramName);

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"El valor no puede exceder {maxLength} caracteres.", paramName);

        return normalized;
    }
}
