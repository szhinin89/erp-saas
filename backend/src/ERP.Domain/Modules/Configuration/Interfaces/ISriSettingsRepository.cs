using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

public interface ISriSettingsRepository
{
    Task<SriSettings?> GetByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Carga SriSettings con SELECT FOR UPDATE. Llamar dentro de una transacción activa.</summary>
    Task<SriSettings?> GetByCompanyIdForUpdateAsync(
        Guid companyId,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(SriSettings config, CancellationToken cancellationToken = default);
    Task UpdateAsync(SriSettings config, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
