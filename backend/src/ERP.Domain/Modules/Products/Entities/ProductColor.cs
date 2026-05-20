using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>Color disponible para este producto (variante de color).</summary>
public class ProductColor : BaseEntity
{
    public Guid   ProductId { get; private set; }
    public string Name      { get; private set; } = null!;
    public string? HexCode  { get; private set; }
    public bool   IsActive  { get; private set; } = true;

    private ProductColor() { }

    internal static ProductColor Create(Guid productId, Guid subscriberId, string name, string? hexCode = null)
        => new()
        {
            Id        = Guid.NewGuid(),
            SubscriberId  = subscriberId,
            ProductId = productId,
            Name      = name.Trim(),
            HexCode   = hexCode?.Trim().ToUpperInvariant(),
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
