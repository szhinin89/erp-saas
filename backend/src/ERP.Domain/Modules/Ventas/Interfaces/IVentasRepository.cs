using ERP.Domain.Modules.Ventas.Entities;

namespace ERP.Domain.Modules.Ventas.Interfaces;

public interface IVentasRepository
{
    Task AddFacturaAsync(VentasFactura factura, CancellationToken ct = default);
    Task<VentasFactura?> GetFacturaByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VentasFactura>> GetFacturasAsync(
        Guid tenantId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        CancellationToken ct = default);
    Task<(IReadOnlyList<VentasFactura> Items, int TotalCount)> GetFacturasPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        Guid? clienteId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        string? search,
        CancellationToken ct = default);

    Task AddNotaCreditoDebitoAsync(VentasNotaCreditoDebito nota, CancellationToken ct = default);
    Task<VentasNotaCreditoDebito?> GetNotaByIdWithDetailsAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VentasNotaCreditoDebito>> GetNotasAsync(
        Guid tenantId,
        Guid? facturaOriginalId,
        string? estado,
        CancellationToken ct = default);

    Task AddRetencionRecibidaAsync(VentasRetencionRecibida retencion, CancellationToken ct = default);
    Task<IReadOnlyList<VentasRetencionRecibida>> GetRetencionesRecibidasAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsRetencionRecibidaClaveAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
