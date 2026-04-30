using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>Talla disponible para este producto (variante de talla: S, M, L, 38, 40…).</summary>
public class ProductSize : BaseEntity
{
    public Guid   ProductId { get; private set; }
    public string Name      { get; private set; } = null!;
    public int    SortOrder { get; private set; }
    public bool   IsActive  { get; private set; } = true;

    private ProductSize() { }

    internal static ProductSize Create(Guid productId, Guid tenantId, string name, int sortOrder = 0)
        => new()
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            ProductId = productId,
            Name      = name.Trim(),
            SortOrder = sortOrder,
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
