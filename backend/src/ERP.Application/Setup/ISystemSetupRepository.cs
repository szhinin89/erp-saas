using ERP.Domain.Setup;

namespace ERP.Application.Setup;

public interface ISystemSetupRepository
{
    Task<SystemSetupState?> GetAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SystemSetupState state, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
