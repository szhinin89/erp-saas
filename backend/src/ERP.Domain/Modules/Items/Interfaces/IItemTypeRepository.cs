using ERP.Domain.Modules.Items.Entities;

namespace ERP.Domain.Modules.Items.Interfaces;

public interface IItemTypeRepository
{
    Task<IReadOnlyList<ItemTypeDefinition>> ListAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default);
    Task<ItemTypeDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ItemTypeDefinition?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> HasActiveItemsAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task AddAsync(ItemTypeDefinition entity, CancellationToken ct = default);
    void Update(ItemTypeDefinition entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
