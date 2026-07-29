using ERP.Application.Setup;
using ERP.Domain.Setup;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SystemSetupRepository : ISystemSetupRepository
{
    private readonly ErpDbContext _db;

    public SystemSetupRepository(ErpDbContext db) => _db = db;

    public Task<SystemSetupState?> GetAsync(CancellationToken cancellationToken = default) =>
        _db.SystemSetupStates.FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(SystemSetupState state, CancellationToken cancellationToken = default)
    {
        _db.SystemSetupStates.Add(state);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
