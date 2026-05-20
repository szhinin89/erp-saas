using ERP.Domain.Common;
using ERP.Domain.Products.Enums;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Campo personalizado extensible sin modificar el esquema.
/// Permite atributos extra por tenant/tipo de producto.
/// </summary>
public class ProductCustomField : BaseEntity
{
    public Guid            ProductId  { get; private set; }
    public string          FieldName  { get; private set; } = null!;
    public CustomFieldType FieldType  { get; private set; }
    public string          FieldValue { get; private set; } = null!;
    public bool            IsActive   { get; private set; } = true;

    private ProductCustomField() { }

    internal static ProductCustomField Create(
        Guid productId, Guid subscriberId, string fieldName, CustomFieldType fieldType, string fieldValue)
        => new()
        {
            Id         = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            ProductId  = productId,
            FieldName  = fieldName.Trim(),
            FieldType  = fieldType,
            FieldValue = fieldValue,
            IsActive   = true,
        };

    public void Disable()
    {
        if (!IsActive)
            throw new InvalidOperationException("El registro ya está deshabilitado.");
        IsActive = false;
    }

    public void Enable()
    {
        if (IsActive)
            throw new InvalidOperationException("El registro ya está activo.");
        IsActive = true;
    }
}
