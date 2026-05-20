using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Dimensión física configurable del producto.
/// Ej: Peso=1.5 KG, Alto=30 CM, Ancho=20 CM, Volumen=0.9 L.
/// </summary>
public class ProductDimension : BaseEntity
{
    public Guid   ProductId { get; private set; }
    public string Name      { get; private set; } = null!;
    public string Value     { get; private set; } = null!;
    public string Unit      { get; private set; } = null!;
    public bool   IsActive  { get; private set; } = true;

    private ProductDimension() { }

    internal static ProductDimension Create(
        Guid productId, Guid subscriberId, string name, string value, string unit)
        => new()
        {
            Id        = Guid.NewGuid(),
            SubscriberId  = subscriberId,
            ProductId = productId,
            Name      = name.Trim(),
            Value     = value.Trim(),
            Unit      = unit.Trim().ToUpperInvariant(),
            IsActive  = true,
        };

    public void Disable()
    {
        if (!IsActive)
            throw new InvalidOperationException("El registro ya está deshabilitado.");
        IsActive = false;
    }

    public void Enable()
    {
        if (IsActive)
            throw new InvalidOperationException("El registro ya está activo.");
        IsActive = true;
    }
}
