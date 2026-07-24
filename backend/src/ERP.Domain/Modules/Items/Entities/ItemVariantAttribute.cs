// CA1711: Name reflects business domain semantics; renaming would break EF mappings, DTOs and handlers.
#pragma warning disable CA1711
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemVariantAttribute : AuditableEntity
{
    public Guid VariantId { get; private set; }
    public Guid AttributeDefinitionId { get; private set; }
    public string Value { get; private set; } = null!;

    private ItemVariantAttribute() { }

    public static ItemVariantAttribute Create(
        Guid variantId, Guid tenantId,
        Guid attributeDefinitionId, string value, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El valor del atributo es obligatorio.", nameof(value));

        var entity = new ItemVariantAttribute
        {
            Id                   = Guid.NewGuid(),
            TenantId         = tenantId,
            VariantId            = variantId,
            AttributeDefinitionId = attributeDefinitionId,
            Value                = value.Trim(),
        };
        entity.SetCreated(createdBy);
        return entity;
    }
}
