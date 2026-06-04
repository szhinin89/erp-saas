using ERP.Domain.Modules.Kits.Entities;

namespace ERP.Domain.Modules.Kits.Interfaces;

public interface IKitRepository
{
    Task<Kit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Kit?> GetByItemIdAsync(Guid itemId, CancellationToken ct = default);
    Task<bool> ExistsByItemIdAsync(Guid itemId, CancellationToken ct = default);
    Task AddAsync(Kit kit, CancellationToken ct = default);
    Task TrackLineAsync(KitLine line, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
