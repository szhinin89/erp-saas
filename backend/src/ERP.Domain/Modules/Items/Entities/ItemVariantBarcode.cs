using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemVariantBarcode : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public string Code { get; private set; } = null!;
    public string BarcodeType { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; }

    private ItemVariantBarcode() { }

    public static ItemVariantBarcode Create(
        Guid itemId,
        Guid tenantId,
        string code,
        string barcodeType,
        Guid createdBy,
        Guid? variantId = null,
        bool isPrimary = false
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de barras es obligatorio.", nameof(code));
        if (code.Length > 100)
            throw new ArgumentException(
                "El código de barras no puede superar 100 caracteres.",
                nameof(code)
            );
        if (string.IsNullOrWhiteSpace(barcodeType))
            throw new ArgumentException(
                "El tipo de código de barras es obligatorio.",
                nameof(barcodeType)
            );

        var entity = new ItemVariantBarcode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = itemId,
            VariantId = variantId,
            Code = code.Trim(),
            BarcodeType = barcodeType.Trim(),
            IsPrimary = isPrimary,
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

    public void Enable() => IsActive = true;

    public void MarkAsPrimary() => IsPrimary = true;

    public void UnmarkAsPrimary() => IsPrimary = false;
}
