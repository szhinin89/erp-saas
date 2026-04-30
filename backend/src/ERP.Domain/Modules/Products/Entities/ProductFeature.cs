using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Característica eCommerce del producto (name/value pair).
/// Ej: "Material: Algodón", "Voltaje: 110V", "Garantía: 1 año".
/// </summary>
public class ProductFeature : BaseEntity
{
    public Guid   ProductId { get; private set; }
    public string Name      { get; private set; } = null!;
    public string Value     { get; private set; } = null!;
    public bool   IsActive  { get; private set; } = true;

    private ProductFeature() { }

    internal static ProductFeature Create(Guid productId, Guid tenantId, string name, string value)
        => new()
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            ProductId = productId,
            Name      = name.Trim(),
            Value     = value.Trim(),
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
