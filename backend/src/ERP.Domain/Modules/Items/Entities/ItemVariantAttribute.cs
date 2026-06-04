using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemVariantAttribute : BaseEntity
{
    public Guid VariantId { get; private set; }
    public Guid AttributeDefinitionId { get; private set; }
    public string Value { get; private set; } = null!;

    private ItemVariantAttribute() { }

    public static ItemVariantAttribute Create(
        Guid variantId, Guid subscriberId,
        Guid attributeDefinitionId, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El valor del atributo es obligatorio.", nameof(value));

        return new ItemVariantAttribute
        {
            Id                   = Guid.NewGuid(),
            SubscriberId         = subscriberId,
            VariantId            = variantId,
            AttributeDefinitionId = attributeDefinitionId,
            Value                = value.Trim(),
        };
    }
}
