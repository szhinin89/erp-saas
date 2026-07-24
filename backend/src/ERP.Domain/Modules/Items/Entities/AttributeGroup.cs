using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Grupo de atributos (ej: "Dimensiones", "Variantes", "eCommerce").
/// tenantId nullable: NULL = grupo global del sistema, UUID = grupo del tenant.
/// </summary>
public sealed class AttributeGroup : MasterEntity, ITenantScopedEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }

    private AttributeGroup() { }

    public static AttributeGroup Create(
        Guid tenantId, string code, string name, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código del grupo es obligatorio.", nameof(code));
        if (code.Length > 50)
            throw new ArgumentException("El código no puede superar 50 caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del grupo es obligatorio.", nameof(name));

        var group = new AttributeGroup
        {
            TenantId = tenantId,
            Code         = code.Trim().ToUpperInvariant(),
            Name         = name.Trim(),
            SortOrder    = sortOrder,
        };
        group.SetCreated(tenantId);
        return group;
    }

    public void Update(string name, int sortOrder, Guid updatedBy)
    {
        Name      = name.Trim();
        SortOrder = sortOrder;
        SetUpdated(updatedBy);
    }
}
