using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface ISriSettingsRepository
{
    Task<SriSettings?> GetByCompanyIdAsync(Guid companyId, CancellationToken ct = default);
    /// <summary>Carga SriSettings con SELECT FOR UPDATE. Llamar dentro de una transacción activa.</summary>
    Task<SriSettings?> GetByCompanyIdForUpdateAsync(Guid companyId, CancellationToken ct = default);
    Task AddAsync(SriSettings config, CancellationToken ct = default);
    Task UpdateAsync(SriSettings config, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}