using ERP.Domain.Modules.Gastos.Entities;
using ERP.Domain.Modules.Gastos.Enums;

namespace ERP.Domain.Modules.Gastos.Interfaces;

public interface IGastoFacturaRepository
{
    Task AddAsync(GastoFactura gasto, CancellationToken ct = default);

    Task<GastoFactura?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<bool> ExistsClaveAccesoAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default);

    Task<IReadOnlyList<GastoFactura>> GetAsync(
        Guid tenantId,
        EstadoGasto? estado,
        Guid? proveedorId,
        DateTime? desde,
        DateTime? hasta,
        string? search,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
