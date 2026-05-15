using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Domain.Modules.Purchasing.Interfaces;

public interface IOrdenCompraRepository
{
    Task AddAsync(OrdenCompra orden, CancellationToken ct = default);

    Task<OrdenCompra?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>Incluye Detalles y vinculaciones OrdenCompraFactura.</summary>
    Task<OrdenCompra?> GetByIdWithDetallesAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<int> GetNextSecuencialAsync(Guid tenantId, CancellationToken ct = default);

    Task<(IReadOnlyList<OrdenCompra> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     proveedorId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default);

    /// <summary>OC en estado Aprobada o RecibidaParcial con saldo pendiente por facturar.</summary>
    Task<IReadOnlyList<OrdenCompra>> GetPendientesPorFacturarAsync(
        Guid tenantId, CancellationToken ct = default);

    Task<bool> FacturaYaVinculadaAsync(
        Guid tenantId, Guid ordenId, Guid facturaId, CancellationToken ct = default);

    Task<IReadOnlyList<(Guid CompraFacturaId, string NumeroFactura, DateTime FechaVinculacion)>>
        GetVinculacionesAsync(Guid tenantId, Guid ordenId, CancellationToken ct = default);

    Task AddOrdenCompraFacturaAsync(OrdenCompraFactura vinculo, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
