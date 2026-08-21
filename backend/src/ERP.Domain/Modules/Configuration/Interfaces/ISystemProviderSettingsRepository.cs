using ERP.Domain.Configuration.Entities;

namespace ERP.Domain.Configuration.Interfaces;

/// <summary>Acceso al singleton (Id = 1) de <see cref="SystemProviderSettings"/>.</summary>
public interface ISystemProviderSettingsRepository
{
    Task<SystemProviderSettings?> GetAsync(CancellationToken cancellationToken = default);
    Task AddAsync(
        SystemProviderSettings settings,
        CancellationToken cancellationToken = default
    );
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
