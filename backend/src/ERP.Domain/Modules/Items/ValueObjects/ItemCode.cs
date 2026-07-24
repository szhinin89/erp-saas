namespace ERP.Domain.Modules.Items.ValueObjects;

/// <summary>
/// Identidad del ítem. SKU es inmutable post-creación.
/// </summary>
public sealed class ItemCode
{
    public string SKU { get; private set; } = null!;
    public string ShortName { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    private ItemCode() { }

    public static ItemCode Create(string sku, string shortName, string description)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("El SKU es obligatorio.", nameof(sku));
        if (sku.Length > 50)
            throw new ArgumentException("El SKU no puede superar 50 caracteres.", nameof(sku));
        if (string.IsNullOrWhiteSpace(shortName))
            throw new ArgumentException("El nombre corto es obligatorio.", nameof(shortName));
        if (shortName.Length > 50)
            throw new ArgumentException("El nombre corto no puede superar 50 caracteres.", nameof(shortName));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripción es obligatoria.", nameof(description));
        if (description.Length > 254)
            throw new ArgumentException("La descripción no puede superar 254 caracteres.", nameof(description));

        return new ItemCode
        {
            SKU         = sku.Trim().ToUpperInvariant(),
            ShortName   = shortName.Trim(),
            Description = description.Trim(),
        };
    }
}
