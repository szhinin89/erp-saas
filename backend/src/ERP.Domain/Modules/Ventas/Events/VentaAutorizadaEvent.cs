using ERP.Domain.Common;

namespace ERP.Domain.Modules.Ventas.Events;

/// <summary>
/// Producto y cantidad vendidos que deben descontarse del stock físico.
/// </summary>
public sealed record VentaAutorizadaStockLine(Guid ProductoId, decimal Cantidad);

/// <summary>
/// Factura de venta autorizada (SRI + asiento); impacto de inventario en handlers.
/// </summary>
public sealed class VentaAutorizadaEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public Guid VentaFacturaId { get; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public Guid BodegaId { get; }
    public string NumeroFactura { get; }
    public IReadOnlyList<VentaAutorizadaStockLine> StockLines { get; }

    public VentaAutorizadaEvent(
        Guid ventaFacturaId,
        Guid tenantId,
        Guid userId,
        Guid bodegaId,
        string numeroFactura,
        IReadOnlyList<VentaAutorizadaStockLine> stockLines)
    {
        VentaFacturaId = ventaFacturaId;
        TenantId       = tenantId;
        UserId         = userId;
        BodegaId       = bodegaId;
        NumeroFactura  = numeroFactura;
        StockLines     = stockLines;
    }
}
