using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Catálogo tenant-editable de tipos de ítem (Físico, Servicio, Digital, Kit, Bundle, ...).
/// Reemplaza el enum cerrado <c>ItemType</c> — el tenant puede crear tipos ilimitados.
/// Sigue exactamente el patrón de <see cref="Sales.Entities.PaymentMethod"/>.
/// </summary>
public sealed class ItemTypeDefinition : MasterEntity, ITenantScopedEntity
{
    public const int MaxCodeLength = 30;
    public const int MaxNameLength = 100;

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }

    private ItemTypeDefinition() { }

    public static ItemTypeDefinition Create(
        Guid tenantId,
        string code,
        string name,
        int sortOrder,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (code.Length > MaxCodeLength)
            throw new ArgumentException(
                $"El código no puede superar {MaxCodeLength} caracteres.",
                nameof(code)
            );
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Length > MaxNameLength)
            throw new ArgumentException(
                $"El nombre no puede superar {MaxNameLength} caracteres.",
                nameof(name)
            );

        var entity = new ItemTypeDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code.Trim(),
            Name = name.Trim(),
            SortOrder = sortOrder,
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>) — Tipo A
    /// (tipos de ítem por defecto), ver política de Bootstrap en <c>CLAUDE.md</c>. Igual que
    /// <see cref="Sales.Entities.PaymentMethod.CreateSystemSeeded"/>: son 5 opciones intercambiables,
    /// no un singleton — deshabilitar una es una decisión de negocio legítima, no una destrucción
    /// accidental. El flag es solo informativo; ningún método invoca
    /// <see cref="SystemSeedGuard.EnsureEditable"/>.
    /// </summary>
    public static ItemTypeDefinition CreateSystemSeeded(
        Guid tenantId,
        string code,
        string name,
        int sortOrder,
        Guid createdBy
    )
    {
        var entity = Create(tenantId, code, name, sortOrder, createdBy);
        entity.MarkAsSystemSeeded();
        return entity;
    }

    public void Update(string name, int sortOrder, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));

        Name = name.Trim();
        SortOrder = sortOrder;
        SetUpdated(updatedBy);
    }
}
