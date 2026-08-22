using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Cabecera de ajuste de inventario. INVENTORY-ADJUSTMENTS-02 — reemplaza el diseño anterior
/// (Producto/Cantidad/Reason-string en la cabecera) por un documento con líneas
/// (<see cref="StockAdjustmentLine"/>) y un motivo administrable (<see cref="InventoryAdjustmentReason"/>).
/// BranchId NO se persiste aquí — se resuelve siempre vía <c>Warehouse.BranchId</c> (mismo
/// convenio que <c>StockTransfer</c>), nunca se confía en la sucursal de sesión del cliente.
/// </summary>
public sealed class StockAdjustment
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public const int NumberMaxLen = 20;
    public const int MovementTypeMaxLen = 10;
    public const int NotesMaxLen = 1000;
    public const int StatusMaxLen = 20;
    public const int NameSnapshotMaxLen = 300;
    public const int CancelledReasonMaxLen = 500;

    public const string MovementTypeIngreso = "Ingreso";
    public const string MovementTypeEgreso = "Egreso";

    public int Sequential { get; private set; }
    public string AdjustmentNumber { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public string WarehouseName { get; private set; } = null!;
    public Guid ReasonId { get; private set; }
    public string MovementType { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTime AdjustmentDate { get; private set; }
    public string Status { get; private set; } = "Draft";
    public DateTime? ExecutedAt { get; private set; }
    public Guid? ExecutedBy { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancelledReason { get; private set; }

    private readonly List<StockAdjustmentLine> _lines = new();
    public IReadOnlyCollection<StockAdjustmentLine> Lines => _lines.AsReadOnly();

    private StockAdjustment() { }

    public static StockAdjustment Create(
        Guid tenantId,
        int sequential,
        Guid warehouseId,
        string warehouseName,
        string movementType,
        Guid reasonId,
        string? notes,
        Guid createdBy,
        Guid companyId
    )
    {
        if (string.IsNullOrWhiteSpace(warehouseName))
            throw new ArgumentException("La bodega es obligatoria.", nameof(warehouseName));
        if (reasonId == Guid.Empty)
            throw new ArgumentException("El motivo del ajuste es obligatorio.", nameof(reasonId));
        EnsureValidMovementType(movementType);

        var a = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Sequential = sequential,
            AdjustmentNumber = $"ADJ-{sequential:D4}",
            WarehouseId = warehouseId,
            WarehouseName = warehouseName.Trim(),
            ReasonId = reasonId,
            MovementType = movementType,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            AdjustmentDate = DateTime.UtcNow,
            Status = "Draft",
        };
        a.SetCreated(createdBy);
        return a;
    }

    /// <summary>Reemplaza cabecera editable (solo Draft) — usado por UpdateStockAdjustment.</summary>
    public void UpdateHeader(
        Guid warehouseId,
        string warehouseName,
        string movementType,
        Guid reasonId,
        string? notes,
        Guid updatedBy
    )
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(warehouseName))
            throw new ArgumentException("La bodega es obligatoria.", nameof(warehouseName));
        if (reasonId == Guid.Empty)
            throw new ArgumentException("El motivo del ajuste es obligatorio.", nameof(reasonId));
        EnsureValidMovementType(movementType);

        WarehouseId = warehouseId;
        WarehouseName = warehouseName.Trim();
        MovementType = movementType;
        ReasonId = reasonId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SetUpdated(updatedBy);
    }

    /// <summary>Reemplaza las líneas del borrador — solo permitido en Draft.</summary>
    public void ReplaceLines(IEnumerable<StockAdjustmentLine> lines)
    {
        EnsureEditable();
        _lines.Clear();
        _lines.AddRange(lines);
    }

    public void Execute(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException(
                $"Only Draft adjustments can be executed (current: {Status})."
            );
        if (_lines.Count == 0)
            throw new InvalidOperationException("El ajuste no tiene líneas.");
        Status = "Executed";
        ExecutedAt = DateTime.UtcNow;
        ExecutedBy = userId;
        SetUpdated(userId);
    }

    /// <summary>
    /// Anula un ajuste ya ejecutado (posteando movimientos inversos — orquestado por el handler,
    /// esta entidad solo administra la transición de estado). No se permite anular un Draft: un
    /// Draft nunca tocó stock, simplemente no se ejecuta.
    /// </summary>
    public void Cancel(string reason, Guid userId)
    {
        if (Status != "Executed")
            throw new InvalidOperationException(
                $"Only Executed adjustments can be cancelled (current: {Status})."
            );
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));

        Status = "Cancelled";
        CancelledReason = reason.Trim();
        CancelledAt = DateTime.UtcNow;
        CancelledBy = userId;
        SetUpdated(userId);
    }

    private void EnsureEditable()
    {
        if (Status != "Draft")
            throw new InvalidOperationException(
                $"Solo un ajuste en Draft puede modificarse (actual: {Status})."
            );
    }

    private static void EnsureValidMovementType(string movementType)
    {
        if (movementType != MovementTypeIngreso && movementType != MovementTypeEgreso)
            throw new ArgumentException(
                $"MovementType debe ser '{MovementTypeIngreso}' o '{MovementTypeEgreso}'.",
                nameof(movementType)
            );
    }
}
