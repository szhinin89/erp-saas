using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Producto sustituto o equivalente.
/// Permite navegar de un producto a sus reemplazos (repuestos, insumos equivalentes de receta, etc.).
/// </summary>
public class ProductSubstitute : BaseEntity
{
    public Guid    ProductId           { get; private set; }
    public Guid    SubstituteProductId { get; private set; }
    public string? Note                { get; private set; }
    public bool    IsActive            { get; private set; } = true;

    private ProductSubstitute() { }

    internal static ProductSubstitute Create(
        Guid productId, Guid tenantId, Guid substituteProductId, string? note = null)
    {
        if (productId == substituteProductId)
            throw new ArgumentException("Un producto no puede ser sustituto de sí mismo.");
        return new()
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            ProductId           = productId,
            SubstituteProductId = substituteProductId,
            Note                = note,
            IsActive            = true,
        };
    }

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
