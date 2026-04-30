using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Conversión entre la unidad base del producto y una unidad alternativa.
/// Ej: 1 CAJA = 12 UND  →  AlternateUnitId = CajaId, Factor = 12.
/// </summary>
public class ProductUnitConversion : BaseEntity
{
    public Guid ProductId         { get; private set; }
    public Guid AlternateUnitId   { get; private set; }
    public decimal ConversionFactor { get; private set; }
    public bool IsActive          { get; private set; } = true;

    private ProductUnitConversion() { }

    internal static ProductUnitConversion Create(
        Guid productId, Guid tenantId, Guid alternateUnitId, decimal factor)
    {
        if (factor <= 0)
            throw new ArgumentException("El factor de conversión debe ser positivo.");
        return new()
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            ProductId         = productId,
            AlternateUnitId   = alternateUnitId,
            ConversionFactor  = factor,
            IsActive          = true,
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
