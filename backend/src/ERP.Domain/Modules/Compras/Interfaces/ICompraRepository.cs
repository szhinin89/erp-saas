using ERP.Domain.Modules.Compras.Entities;
using ERP.Domain.Modules.Compras.Enums;

namespace ERP.Domain.Modules.Compras.Interfaces;

public interface ICompraRepository
{
    Task AddAsync(CompraFactura compra, CancellationToken ct = default);
    Task<CompraFactura?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<CompraFactura?> GetByIdWithDetailsAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsClaveAccesoAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default);
    Task<IReadOnlyList<CompraFactura>> GetAsync(
        Guid tenantId,
        EstadoCompra? estado,
        Guid?        proveedorId,
        DateTime?    desde,
        DateTime?    hasta,
        string?      search,
        CancellationToken ct = default);

    Task<IReadOnlyList<CompraBodegaAsignacion>> GetBodegaAsignacionesByCompraFacturaIdAsync(
        Guid tenantId,
        Guid compraFacturaId,
        CancellationToken ct = default);

    Task AddBodegaAsignacionAsync(CompraBodegaAsignacion asignacion, CancellationToken ct = default);

    Task AddRetencionEmitidaAsync(CompraRetencionEmitida retencion, CancellationToken ct = default);
    Task<CompraRetencionEmitida?> GetRetencionEmitidaByIdWithDetailsAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CompraRetencionEmitida>> GetRetencionesEmitidasAsync(Guid tenantId, Guid? proveedorId, CancellationToken ct = default);

    Task AddNotaProveedorAsync(CompraNotaProveedor nota, CancellationToken ct = default);
    Task<CompraNotaProveedor?> GetNotaProveedorByIdWithDetailsAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsNotaProveedorClaveAccesoAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default);
    Task<IReadOnlyList<CompraNotaProveedor>> GetNotasProveedorAsync(
        Guid tenantId,
        Guid? proveedorId,
        Guid? compraFacturaId,
        Guid? gastoFacturaId,
        string? estado,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
