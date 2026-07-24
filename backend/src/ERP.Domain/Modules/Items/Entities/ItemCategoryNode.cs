using ERP.Domain.Common;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.Domain.Modules.Items.Entities;

public sealed class ItemCategoryNode : MasterEntity, ITenantScopedEntity
{
    /// <summary>
    /// Código reservado del nodo raíz "No aplica" que crea el Bootstrap de empresa (catálogo
    /// Tipo C — ver política de Bootstrap en <c>CLAUDE.md</c>). Único, protegido: no editable,
    /// no reparentable ni deshabilitable.
    /// </summary>
    public const string NoAplicaCode = "NO_APLICA";

    public Guid? ParentId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public CategoryNodeLevel Level { get; private set; }
    public string Path { get; private set; } = "/";
    public int SortOrder { get; private set; }

    private ItemCategoryNode() { }

    public static ItemCategoryNode Create(
        Guid tenantId,
        string code,
        string name,
        CategoryNodeLevel level,
        Guid createdBy,
        Guid? parentId = null,
        string? description = null,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código del nodo es obligatorio.", nameof(code));
        if (code.Length > 20)
            throw new ArgumentException("El código no puede superar 20 caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del nodo es obligatorio.", nameof(name));
        if (name.Length > 120)
            throw new ArgumentException("El nombre no puede superar 120 caracteres.", nameof(name));

        var node = new ItemCategoryNode
        {
            TenantId    = tenantId,
            ParentId    = parentId,
            Code        = code.Trim().ToUpperInvariant(),
            Name        = name.Trim(),
            Description = description?.Trim() is { Length: > 0 } d ? d : null,
            Level       = level,
            SortOrder   = sortOrder,
        };
        node.SetCreated(createdBy);
        return node;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Update"/>, <see cref="Disable"/> y <see cref="Reparent"/> — catálogo Tipo C, ver
    /// política de Bootstrap en <c>CLAUDE.md</c>.
    /// </summary>
    public static ItemCategoryNode CreateSystemSeeded(
        Guid tenantId,
        string code,
        string name,
        CategoryNodeLevel level,
        Guid createdBy,
        Guid? parentId = null,
        string? description = null,
        int sortOrder = 0)
    {
        var node = Create(tenantId, code, name, level, createdBy, parentId, description, sortOrder);
        node.MarkAsSystemSeeded();
        return node;
    }

    public void Update(string code, string name, Guid updatedBy, string? description = null, int? sortOrder = null)
    {
        this.EnsureEditable("El nodo de categoría", "modificarse");
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim() is { Length: > 0 } d ? d : null;
        if (sortOrder.HasValue) SortOrder = sortOrder.Value;
        SetUpdated(updatedBy);
    }

    public void SetPath(string path) => Path = path;

    public void Reparent(Guid? newParentId, CategoryNodeLevel newLevel, Guid updatedBy)
    {
        this.EnsureEditable("El nodo de categoría", "reubicarse");

        ParentId = newParentId;
        Level = newLevel;
        SetUpdated(updatedBy);
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("El nodo de categoría", "deshabilitarse");

        base.Disable(updatedBy);
    }
}
