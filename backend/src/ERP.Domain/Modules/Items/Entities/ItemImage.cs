using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemImage : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public Guid StorageObjectId { get; private set; }
    public string? AltText { get; private set; }
    public bool IsMain { get; private set; }
    public bool IsEcommerce { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private ItemImage() { }

    public static ItemImage Create(
        Guid itemId, Guid tenantId,
        Guid storageObjectId, string? altText,
        bool isMain, bool isEcommerce, int sortOrder,
        Guid createdBy,
        Guid? variantId = null)
    {
        var entity = new ItemImage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = itemId,
            VariantId = variantId,
            StorageObjectId = storageObjectId,
            AltText = altText?.Trim(),
            IsMain = isMain,
            IsEcommerce = isEcommerce,
            SortOrder = sortOrder,
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
