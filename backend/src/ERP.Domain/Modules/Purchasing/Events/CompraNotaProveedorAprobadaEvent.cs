using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Events;

/// <summary>Línea de inventario al aprobar nota de proveedor vinculada a compra (delta: + aumenta stock en NC, − disminuye en ND).</summary>
public sealed record CompraNotaProveedorStockLine(
    Guid ProductoId,
    Guid BodegaId,
    decimal DeltaCantidad,
    decimal CostoUnitario);

/// <summary>Nota de proveedor aprobada; el handler de inventario aplica solo si hay líneas y compra vinculada.</summary>
public sealed class CompraNotaProveedorAprobadaEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public Guid NotaId { get; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public Guid CompraFacturaId { get; }
    public string TipoNota { get; }
    public string NumeroNota { get; }
    public IReadOnlyList<CompraNotaProveedorStockLine> StockLines { get; }

    public CompraNotaProveedorAprobadaEvent(
        Guid notaId,
        Guid tenantId,
        Guid userId,
        Guid compraFacturaId,
        string tipoNota,
        string numeroNota,
        IReadOnlyList<CompraNotaProveedorStockLine> stockLines)
    {
        NotaId           = notaId;
        TenantId         = tenantId;
        UserId           = userId;
        CompraFacturaId  = compraFacturaId;
        TipoNota         = tipoNota;
        NumeroNota       = numeroNota;
        StockLines       = stockLines;
    }
}
