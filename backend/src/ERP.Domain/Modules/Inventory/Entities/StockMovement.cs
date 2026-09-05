using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class StockMovement : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public const int ReferenceMaxLen = 100;
    public const int SourceDocTypeMaxLen = 50;
    public const int UomCodeMaxLen = 20;

    /// <summary>
    /// Sucursal dueña del hecho físico de inventario (Branch Ownership) — SIEMPRE
    /// <c>Warehouse.BranchId</c> de <see cref="WarehouseId"/>, nunca la sucursal de sesión activa
    /// del usuario que originó la operación. En una transferencia inter-sucursal, el movimiento
    /// del lado destino lleva el BranchId de la bodega destino, no el de origen — un mismo
    /// documento puede generar dos StockMovement con BranchId distinto.
    /// </summary>
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; }
    public string UomCode { get; private set; } = string.Empty;
    public decimal PreviousQuantity { get; private set; }
    public decimal ResultQuantity { get; private set; }
    public long SequenceNumber { get; private set; }
    public string? Reference { get; private set; }
    public Guid? SourceDocId { get; private set; }
    public string? SourceDocType { get; private set; }

    /// <summary>
    /// Referencia genérica (no específica de Compras) a la línea del documento origen del
    /// movimiento — P0-02, diseño §10.3. Permite identificar sin ambigüedad qué línea concreta de
    /// qué documento originó este movimiento, incluso cuando dos líneas comparten producto y
    /// bodega pero difieren en costo. No es la fuente de "cantidad ya devuelta" (eso es una
    /// consulta derivada de dominio de negocio) — es trazabilidad de kardex/auditoría, reutilizable
    /// por cualquier módulo futuro que necesite trazabilidad línea-a-línea.
    /// </summary>
    public Guid? SourceDocLineId { get; private set; }
    public decimal? UnitCost { get; private set; }
    public decimal? TotalCost { get; private set; }
    public decimal RunningAverageCost { get; private set; }
    public decimal RunningStockValue { get; private set; }
    public DateOnly EffectiveDate { get; private set; }

    // ── Extensiones futuras (sin lógica en esta fase) ───────────────────
    public Guid? LotId { get; private set; }
    public Guid? SerialId { get; private set; }
    public Guid? AccountingTransactionId { get; private set; }

    private StockMovement() { }

    /// <summary>
    /// Factory de bajo nivel. El cálculo de <see cref="SequenceNumber"/>, <see cref="RunningAverageCost"/>
    /// y <see cref="RunningStockValue"/> es responsabilidad exclusiva de
    /// <c>IStockRepository.AppendMovementAsync</c> — nunca se recalculan a partir de CurrentStock.
    /// </summary>
    /// <param name="unitCost">
    /// Costo unitario CAPTURADO (manual/de compra) para este movimiento — solo tiene valor cuando
    /// el caller efectivamente lo provee (p. ej. una entrada). Nunca se infiere de un promedio;
    /// una salida (venta, ajuste negativo) SIEMPRE lo deja en <c>null</c> — es la semántica que
    /// distingue "costo tecleado" de "costo de valuación resuelto internamente".
    /// </param>
    /// <param name="valuationUnitCost">
    /// ACCOUNTING-INVENTORY-COGS-07 / TECH-DEBT-API-INVENTORY-ADJUSTMENT-FAILURE-01A — costo de
    /// valuación ya resuelto por el caller (típicamente el costo promedio corrido vigente ANTES
    /// del movimiento) usado ÚNICAMENTE para calcular <see cref="TotalCost"/> cuando
    /// <paramref name="unitCost"/> es <c>null</c> — nunca sobrescribe <see cref="UnitCost"/>. Así
    /// una salida puede tener <c>TotalCost</c> (necesario para costear COGS en Accounting) sin que
    /// eso implique que capturó un costo manual.
    /// </param>
    public static StockMovement Create(
        Guid tenantId,
        Guid branchId,
        Guid productId,
        Guid warehouseId,
        StockMovementType movementType,
        decimal quantity,
        string uomCode,
        decimal previousQuantity,
        long sequenceNumber,
        decimal runningAverageCost,
        decimal runningStockValue,
        DateOnly effectiveDate,
        string? reference,
        Guid? sourceDocId,
        string? sourceDocType,
        Guid createdBy,
        Guid companyId,
        decimal? unitCost = null,
        Guid? lotId = null,
        Guid? serialId = null,
        Guid? sourceDocLineId = null,
        decimal? valuationUnitCost = null
    )
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (movementType == StockMovementType.PurchaseEntry && (unitCost is null or <= 0))
            throw new InvalidOperationException(
                "El costo unitario es obligatorio y debe ser mayor a cero para entradas de compra."
            );
        if (sequenceNumber <= 0)
            throw new InvalidOperationException("SequenceNumber debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new InvalidOperationException(
                "UomCode es obligatorio para un movimiento de Kardex."
            );

        var costForTotal = unitCost ?? valuationUnitCost;

        var m = new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = movementType,
            Quantity = quantity,
            UomCode = uomCode.Trim(),
            PreviousQuantity = previousQuantity,
            ResultQuantity = previousQuantity + quantity,
            SequenceNumber = sequenceNumber,
            RunningAverageCost = runningAverageCost,
            RunningStockValue = runningStockValue,
            EffectiveDate = effectiveDate,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            SourceDocId = sourceDocId,
            SourceDocType = string.IsNullOrWhiteSpace(sourceDocType) ? null : sourceDocType.Trim(),
            SourceDocLineId = sourceDocLineId,
            UnitCost = unitCost,
            TotalCost = costForTotal.HasValue ? Math.Abs(quantity) * costForTotal.Value : null,
            LotId = lotId,
            SerialId = serialId,
        };
        m.SetCreated(createdBy);
        return m;
    }
}
