using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Events;

/// <summary>
/// ACCOUNTING-12A — un ajuste de inventario (Ingreso/Egreso) se ejecutó y su Kardex quedó registrado.
/// Es el hecho económico; Accounting decide si genera obligación y su importe sale del TotalCost real de
/// los movimientos del documento y su fecha es la fecha efectiva de esos movimientos (la ejecución, no la
/// creación del borrador). El saldo inicial de Carga Inicial no lo publica (tiene su propio hecho).
/// </summary>
public sealed class StockAdjustmentExecutedEvent : BaseDomainEvent
{
    public Guid CompanyId { get; }
    public Guid StockAdjustmentId { get; }
    public string AdjustmentNumber { get; }
    public string MovementType { get; }
    public Guid ReasonId { get; }

    public StockAdjustmentExecutedEvent(Guid tenantId, Guid companyId, Guid stockAdjustmentId,
        string adjustmentNumber, string movementType, Guid reasonId)
    {
        TenantId = tenantId; CompanyId = companyId; StockAdjustmentId = stockAdjustmentId;
        AdjustmentNumber = adjustmentNumber; MovementType = movementType; ReasonId = reasonId;
    }
}

/// <summary>ACCOUNTING-12A — un ajuste ejecutado se anuló (su Kardex se revirtió con movimientos inversos).</summary>
public sealed class StockAdjustmentCancelledEvent : BaseDomainEvent
{
    public Guid CompanyId { get; }
    public Guid StockAdjustmentId { get; }
    public string MovementType { get; }
    public string CancelReason { get; }

    public StockAdjustmentCancelledEvent(Guid tenantId, Guid companyId, Guid stockAdjustmentId,
        string movementType, string cancelReason)
    {
        TenantId = tenantId; CompanyId = companyId; StockAdjustmentId = stockAdjustmentId;
        MovementType = movementType; CancelReason = cancelReason;
    }
}
