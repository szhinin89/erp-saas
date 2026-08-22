using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Catálogo administrable de motivos de ajuste de inventario (SSOT dinámico — CLAUDE.md § Catálogos
/// y datos configurables). INVENTORY-ADJUSTMENTS-02. <see cref="CompanyId"/> es nullable: null =
/// motivo disponible para todo el tenant (todas las empresas), valor concreto = motivo restringido
/// a esa empresa. No existe un precedente exacto de "catálogo nullable-company" en el código — se
/// modela como columna nullable simple, sin FK obligatoria.
/// </summary>
public sealed class InventoryAdjustmentReason : MasterEntity, ITenantScopedEntity
{
    public const int CodeMaxLen = 30;
    public const int NameMaxLen = 150;
    public const int MovementTypeMaxLen = 10;

    public const string Ingreso = "Ingreso";
    public const string Egreso = "Egreso";
    public const string Ambos = "Ambos";

    /// <summary>Null = motivo disponible para todas las empresas del tenant (tenant-wide).</summary>
    public Guid? CompanyId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string AllowedMovementType { get; private set; } = null!;
    public bool RequiresNotes { get; private set; }
    public int SortOrder { get; private set; }

    private InventoryAdjustmentReason() { }

    public static InventoryAdjustmentReason Create(
        Guid tenantId,
        Guid? companyId,
        string code,
        string name,
        string allowedMovementType,
        bool requiresNotes,
        int sortOrder,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        EnsureValidMovementType(allowedMovementType);

        var r = new InventoryAdjustmentReason
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            AllowedMovementType = allowedMovementType,
            RequiresNotes = requiresNotes,
            SortOrder = sortOrder,
        };
        r.SetCreated(createdBy);
        return r;
    }

    /// <summary>Code es inmutable tras la creación — mismo convenio que otros catálogos.</summary>
    public void Update(
        string name,
        string allowedMovementType,
        bool requiresNotes,
        int sortOrder,
        Guid updatedBy
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        EnsureValidMovementType(allowedMovementType);

        Name = name.Trim();
        AllowedMovementType = allowedMovementType;
        RequiresNotes = requiresNotes;
        SortOrder = sortOrder;
        SetUpdated(updatedBy);
    }

    public bool AllowsMovementType(string movementType) =>
        AllowedMovementType == Ambos || AllowedMovementType == movementType;

    private static void EnsureValidMovementType(string allowedMovementType)
    {
        if (allowedMovementType != Ingreso && allowedMovementType != Egreso && allowedMovementType != Ambos)
            throw new ArgumentException(
                $"AllowedMovementType debe ser '{Ingreso}', '{Egreso}' o '{Ambos}'.",
                nameof(allowedMovementType)
            );
    }
}
