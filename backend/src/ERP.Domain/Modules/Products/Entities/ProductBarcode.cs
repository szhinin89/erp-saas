using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Código de barras de un producto. Un producto puede tener múltiples.
/// Tipos: EAN13, EAN8, QR, Code128, interno, etc.
/// </summary>
public class ProductBarcode : BaseEntity
{
    public Guid ProductId { get; private set; }
    public string Code { get; private set; } = null!;
    public BarcodeType Type { get; private set; }

    private ProductBarcode() { }

    internal static ProductBarcode Create(
        Guid productId,
        Guid tenantId,
        string code,
        BarcodeType type)
    {
        return new ProductBarcode
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            ProductId = productId,
            Code      = code,
            Type      = type,
        };
    }
}
