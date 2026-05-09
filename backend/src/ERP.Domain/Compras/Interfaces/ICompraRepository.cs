using ERP.Domain.Compras.Entities;
using ERP.Domain.Compras.Enums;

namespace ERP.Domain.Compras.Interfaces;

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
    Task SaveChangesAsync(CancellationToken ct = default);
}
