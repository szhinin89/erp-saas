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
    Task SaveChangesAsync(CancellationToken ct = default);
}
