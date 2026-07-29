using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Nivel de empaque logístico: Unidad → Caja → Bulto → Pallet.
/// BaseQuantity indica cuántas unidades base contiene este nivel.
/// </summary>
public sealed class ItemPackagingLevel : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public string Name { get; private set; } = null!;
    public int Level { get; private set; }
    public decimal BaseQuantity { get; private set; }
    public string UomCode { get; private set; } = null!;
    public string? Barcode { get; private set; }
    public decimal? Weight { get; private set; }
    public bool IsBaseUnit { get; private set; }
    public bool IsPurchaseDefault { get; private set; }
    public bool IsSaleDefault { get; private set; }
    public bool IsActive { get; private set; }

    private ItemPackagingLevel() { }

    public static ItemPackagingLevel Create(
        Guid itemId, Guid tenantId,
        string name, int level, decimal baseQuantity, string uomCode,
        string? barcode, decimal? weight,
        bool isBaseUnit, bool isPurchaseDefault, bool isSaleDefault, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del nivel de empaque es obligatorio.", nameof(name));
        if (baseQuantity <= 0)
            throw new ArgumentException("La cantidad base debe ser mayor que cero.", nameof(baseQuantity));
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new ArgumentException("El código UOM es obligatorio.", nameof(uomCode));

        var entity = new ItemPackagingLevel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = itemId,
            Name = name.Trim(),
            Level = level,
            BaseQuantity = baseQuantity,
            UomCode = uomCode.Trim().ToUpperInvariant(),
            Barcode = barcode?.Trim(),
            Weight = weight,
            IsBaseUnit = isBaseUnit,
            IsPurchaseDefault = isPurchaseDefault,
            IsSaleDefault = isSaleDefault,
            IsActive = true,
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    public void Disable(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }
}
