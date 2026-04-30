using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Registro de arancel de importación por país de origen.
/// Un producto puede tener aranceles de múltiples países.
/// </summary>
public class ProductTariffDetail : BaseEntity
{
    public Guid    ProductId      { get; private set; }
    public string  OriginCountry  { get; private set; } = null!;
    public string  TariffCode     { get; private set; } = null!;
    public decimal Percentage     { get; private set; }
    public bool    IsActive       { get; private set; } = true;

    private ProductTariffDetail() { }

    internal static ProductTariffDetail Create(
        Guid productId, Guid tenantId, string originCountry, string tariffCode, decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentException("El porcentaje arancelario debe estar entre 0 y 100.");
        return new()
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            ProductId     = productId,
            OriginCountry = originCountry.Trim().ToUpperInvariant(),
            TariffCode    = tariffCode.Trim(),
            Percentage    = percentage,
            IsActive      = true,
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
