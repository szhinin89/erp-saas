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
    public bool IsActive { get; private set; } = true;

    private ProductBarcode() { }

    internal static ProductBarcode Create(
        Guid productId,
        Guid subscriberId,
        string code,
        BarcodeType type)
    {
        return new ProductBarcode
        {
            Id        = Guid.NewGuid(),
            SubscriberId  = subscriberId,
            ProductId = productId,
            Code      = code,
            Type      = type,
            IsActive  = true,
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
