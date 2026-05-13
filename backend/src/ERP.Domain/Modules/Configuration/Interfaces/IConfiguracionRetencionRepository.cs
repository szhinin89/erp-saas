using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface IConfiguracionRetencionRepository
{
    Task<IReadOnlyList<ConfiguracionRetencion>> GetActivosParaProveedorAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(ConfiguracionRetencion entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
