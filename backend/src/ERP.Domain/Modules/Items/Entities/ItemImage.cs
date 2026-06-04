using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemImage : BaseEntity
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
        Guid itemId, Guid subscriberId,
        Guid storageObjectId, string? altText,
        bool isMain, bool isEcommerce, int sortOrder,
        Guid? variantId = null)
    {
        return new ItemImage
        {
            Id              = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            ItemId          = itemId,
            VariantId       = variantId,
            StorageObjectId = storageObjectId,
            AltText         = altText?.Trim(),
            IsMain          = isMain,
            IsEcommerce     = isEcommerce,
            SortOrder       = sortOrder,
            IsActive        = true,
        };
    }

    public void Disable() => IsActive = false;
}
