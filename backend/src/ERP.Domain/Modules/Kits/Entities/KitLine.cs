using ERP.Domain.Common;

namespace ERP.Domain.Modules.Kits.Entities;

public sealed class KitLine : BaseEntity
{
    public Guid KitId { get; private set; }
    public Guid ComponentItemId { get; private set; }
    public Guid? ComponentVariantId { get; private set; }
    public decimal Quantity { get; private set; }
    public string UomCode { get; private set; } = null!;
    public bool IsOptional { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private KitLine() { }

    internal static KitLine Create(
        Guid kitId, Guid subscriberId,
        Guid componentItemId, Guid? componentVariantId,
        decimal quantity, string uomCode,
        bool isOptional, int sortOrder)
    {
        if (quantity <= 0)
            throw new ArgumentException("La cantidad del componente debe ser mayor que cero.", nameof(quantity));
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new ArgumentException("El código UOM es obligatorio.", nameof(uomCode));

        return new KitLine
        {
            Id                 = Guid.NewGuid(),
            SubscriberId       = subscriberId,
            KitId              = kitId,
            ComponentItemId    = componentItemId,
            ComponentVariantId = componentVariantId,
            Quantity           = quantity,
            UomCode            = uomCode.Trim().ToUpperInvariant(),
            IsOptional         = isOptional,
            SortOrder          = sortOrder,
            IsActive           = true,
        };
    }

    internal void Update(decimal quantity, string uomCode, bool isOptional, int sortOrder)
    {
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor que cero.", nameof(quantity));

        Quantity  = quantity;
        UomCode   = uomCode.Trim().ToUpperInvariant();
        IsOptional = isOptional;
        SortOrder = sortOrder;
    }

    public void Disable() => IsActive = false;
}
