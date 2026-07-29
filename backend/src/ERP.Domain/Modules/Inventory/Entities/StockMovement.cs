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
        Guid? serialId = null
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
            UnitCost = unitCost,
            TotalCost = unitCost.HasValue ? Math.Abs(quantity) * unitCost.Value : null,
            LotId = lotId,
            SerialId = serialId,
        };
        m.SetCreated(createdBy);
        return m;
    }
}
