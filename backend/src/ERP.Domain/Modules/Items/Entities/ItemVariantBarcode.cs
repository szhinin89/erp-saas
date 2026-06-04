using ERP.Domain.Common;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemVariantBarcode : BaseEntity
{
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public string Code { get; private set; } = null!;
    public BarcodeType BarcodeType { get; private set; }
    public bool IsActive { get; private set; }

    private ItemVariantBarcode() { }

    public static ItemVariantBarcode Create(
        Guid itemId, Guid subscriberId,
        string code, BarcodeType barcodeType,
        Guid? variantId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de barras es obligatorio.", nameof(code));
        if (code.Length > 100)
            throw new ArgumentException("El código de barras no puede superar 100 caracteres.", nameof(code));

        return new ItemVariantBarcode
        {
            Id          = Guid.NewGuid(),
            SubscriberId = subscriberId,
            ItemId      = itemId,
            VariantId   = variantId,
            Code        = code.Trim(),
            BarcodeType = barcodeType,
            IsActive    = true,
        };
    }

    public void Disable() => IsActive = false;
    public void Enable()  => IsActive = true;
}
