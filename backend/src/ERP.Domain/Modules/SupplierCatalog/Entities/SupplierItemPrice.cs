using ERP.Domain.Common;

namespace ERP.Domain.Modules.SupplierCatalog.Entities;

public sealed class SupplierItemPrice : BaseEntity
{
    public Guid SupplierItemId { get; private set; }
    public decimal MinQty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public bool IsActive { get; private set; }

    private SupplierItemPrice() { }

    internal static SupplierItemPrice Create(
        Guid supplierItemId, Guid subscriberId,
        decimal minQty, decimal unitPrice, string currencyCode,
        DateOnly? validFrom = null, DateOnly? validTo = null)
    {
        if (minQty <= 0)
            throw new ArgumentException("La cantidad mínima debe ser mayor que cero.", nameof(minQty));
        if (unitPrice < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(unitPrice));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new ArgumentException("El código de moneda debe tener 3 caracteres.", nameof(currencyCode));
        if (validTo.HasValue && validFrom.HasValue && validTo < validFrom)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");

        return new SupplierItemPrice
        {
            Id             = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            SupplierItemId = supplierItemId,
            MinQty         = minQty,
            UnitPrice      = unitPrice,
            CurrencyCode   = currencyCode.Trim().ToUpperInvariant(),
            ValidFrom      = validFrom,
            ValidTo        = validTo,
            IsActive       = true,
        };
    }

    public void Disable() => IsActive = false;
}
