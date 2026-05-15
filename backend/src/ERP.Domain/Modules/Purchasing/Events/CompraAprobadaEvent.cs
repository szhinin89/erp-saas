using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Events;

/// <summary>
/// Línea de impacto a inventario al aprobar una compra (asignación bodega–producto).
/// </summary>
public sealed record CompraAprobadaStockLine(
    Guid CompraDetalleId,
    Guid? ProductoId,
    Guid BodegaId,
    decimal Cantidad,
    decimal CostoUnitarioNeto);

/// <summary>
/// La compra quedó aprobada en base de datos; los handlers aplican entradas de stock y kardex.
/// </summary>
public sealed class CompraAprobadaEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public Guid CompraFacturaId { get; }
    public Guid TenantId { get; }
    public string NumeroFactura { get; }
    public Guid ApprovedByUserId { get; }
    public IReadOnlyList<CompraAprobadaStockLine> StockLines { get; }

    public CompraAprobadaEvent(
        Guid compraFacturaId,
        Guid tenantId,
        string numeroFactura,
        Guid approvedByUserId,
        IReadOnlyList<CompraAprobadaStockLine> stockLines)
    {
        CompraFacturaId   = compraFacturaId;
        TenantId          = tenantId;
        NumeroFactura     = numeroFactura;
        ApprovedByUserId  = approvedByUserId;
        StockLines        = stockLines;
    }
}
