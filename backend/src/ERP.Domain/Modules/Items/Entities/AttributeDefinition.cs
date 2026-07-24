using ERP.Domain.Common;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Definición tipada de un atributo (ej: Color, Talla, Peso).
/// IsVariantAxis=true → el atributo define una dimensión de variante.
/// AllowedValues: JSON serializado [{value, label}] para DataType=List.
/// </summary>
public sealed class AttributeDefinition : MasterEntity, ITenantScopedEntity
{
    public Guid GroupId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public AttributeDataType DataType { get; private set; }
    public bool IsVariantAxis { get; private set; }
    public string? AllowedValues { get; private set; }
    public bool IsRequired { get; private set; }
    public int SortOrder { get; private set; }

    private AttributeDefinition() { }

    public static AttributeDefinition Create(
        Guid tenantId,
        Guid groupId,
        string code,
        string name,
        AttributeDataType dataType,
        bool isVariantAxis = false,
        string? allowedValues = null,
        bool isRequired = false,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código del atributo es obligatorio.", nameof(code));
        if (code.Length > 50)
            throw new ArgumentException("El código no puede superar 50 caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del atributo es obligatorio.", nameof(name));
        if (dataType == AttributeDataType.List && string.IsNullOrWhiteSpace(allowedValues))
            throw new ArgumentException("Debe especificar los valores permitidos para atributos de tipo List.", nameof(allowedValues));

        var def = new AttributeDefinition
        {
            TenantId  = tenantId,
            GroupId       = groupId,
            Code          = code.Trim().ToUpperInvariant(),
            Name          = name.Trim(),
            DataType      = dataType,
            IsVariantAxis = isVariantAxis,
            AllowedValues = allowedValues,
            IsRequired    = isRequired,
            SortOrder     = sortOrder,
        };
        def.SetCreated(tenantId);
        return def;
    }

    public void Update(
        string name, bool isVariantAxis, string? allowedValues,
        bool isRequired, int sortOrder, Guid updatedBy)
    {
        if (DataType == AttributeDataType.List && string.IsNullOrWhiteSpace(allowedValues))
            throw new ArgumentException("Debe especificar los valores permitidos para atributos de tipo List.", nameof(allowedValues));

        Name          = name.Trim();
        IsVariantAxis = isVariantAxis;
        AllowedValues = allowedValues;
        IsRequired    = isRequired;
        SortOrder     = sortOrder;
        SetUpdated(updatedBy);
    }
}
