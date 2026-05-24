using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>Código del producto asignado por un proveedor específico (1:N por producto).</summary>
public class ProductSupplierCode : BaseEntity
{
    public Guid ProductId         { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public string Code     { get; private set; } = null!;
    public bool IsDefault  { get; private set; }
    public bool IsActive   { get; private set; } = true;

    private ProductSupplierCode() { }

    internal static ProductSupplierCode Create(
        Guid productId, Guid subscriberId, Guid businessPartnerId, string code, bool isDefault = false)
        => new()
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            ProductId         = productId,
            BusinessPartnerId = businessPartnerId,
            Code       = code.Trim(),
            IsDefault  = isDefault,
            IsActive   = true,
        };

    public void Disable()
    {
        if (!IsActive)
            throw new InvalidOperationException("El registro ya está deshabilitado.");
        IsActive = false;
        IsDefault = false;
    }

    public void Enable()
    {
        if (IsActive)
            throw new InvalidOperationException("El registro ya está activo.");
        IsActive = true;
    }
}
