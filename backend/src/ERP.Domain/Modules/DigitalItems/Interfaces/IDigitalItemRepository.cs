using ERP.Domain.Modules.DigitalItems.Entities;

namespace ERP.Domain.Modules.DigitalItems.Interfaces;

public interface IDigitalItemRepository
{
    Task<DigitalItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DigitalItem?> GetByItemIdAsync(Guid itemId, CancellationToken ct = default);
    Task<bool> ExistsByItemIdAsync(Guid itemId, CancellationToken ct = default);
    Task AddAsync(DigitalItem item, CancellationToken ct = default);
    Task TrackDeliverableAsync(DigitalDeliverable deliverable, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
