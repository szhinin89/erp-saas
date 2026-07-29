using ERP.Domain.Modules.Items.Entities;

namespace ERP.Domain.Modules.Items.Interfaces;

public interface ICategoryNodeRepository
{
    Task<ItemCategoryNode?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ItemCategoryNode>> GetAllAsync(
        Guid tenantId,
        bool includeInactive = false,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ItemCategoryNode>> GetChildrenAsync(
        Guid parentId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ItemCategoryNode>> GetDescendantsByPathAsync(
        Guid tenantId,
        string pathPrefix,
        CancellationToken ct = default
    );
    Task<bool> CodeExistsAsync(
        string code,
        Guid tenantId,
        Guid? excludeId = null,
        CancellationToken ct = default
    );
    Task<bool> HasActiveChildrenAsync(Guid nodeId, CancellationToken ct = default);
    Task<bool> AnyAncestorDisabledAsync(Guid nodeId, CancellationToken ct = default);
    Task<int> CountItemsByNodeAsync(Guid nodeId, CancellationToken ct = default);
    Task AddAsync(ItemCategoryNode node, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
