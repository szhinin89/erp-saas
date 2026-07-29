using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class CategoryNodeRepository : ICategoryNodeRepository
{
    private readonly ErpDbContext _ctx;

    public CategoryNodeRepository(ErpDbContext ctx) => _ctx = ctx;

    public async Task<ItemCategoryNode?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _ctx.Set<ItemCategoryNode>().FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<ItemCategoryNode>> GetAllAsync(
        Guid tenantId,
        bool includeInactive = false,
        CancellationToken ct = default
    )
    {
        var q = _ctx.Set<ItemCategoryNode>().Where(n => n.TenantId == tenantId);
        if (!includeInactive)
            q = q.Where(n => n.IsActive);
        return await q.OrderBy(n => n.Path).ThenBy(n => n.SortOrder).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ItemCategoryNode>> GetChildrenAsync(
        Guid parentId,
        CancellationToken ct = default
    ) =>
        await _ctx.Set<ItemCategoryNode>()
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ItemCategoryNode>> GetDescendantsByPathAsync(
        Guid tenantId,
        string pathPrefix,
        CancellationToken ct = default
    ) =>
        await _ctx.Set<ItemCategoryNode>()
            .Where(n => n.TenantId == tenantId && n.Path.StartsWith(pathPrefix))
            .OrderBy(n => n.Path)
            .ToListAsync(ct);

    public async Task<bool> CodeExistsAsync(
        string code,
        Guid tenantId,
        Guid? excludeId = null,
        CancellationToken ct = default
    )
    {
        var normalized = code.Trim().ToUpperInvariant();
        var q = _ctx.Set<ItemCategoryNode>()
            .Where(n => n.TenantId == tenantId && n.Code == normalized);
        if (excludeId.HasValue)
            q = q.Where(n => n.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task<bool> HasActiveChildrenAsync(Guid nodeId, CancellationToken ct = default) =>
        await _ctx.Set<ItemCategoryNode>().AnyAsync(n => n.ParentId == nodeId && n.IsActive, ct);

    public async Task<bool> AnyAncestorDisabledAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _ctx.Set<ItemCategoryNode>()
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, ct);
        if (node is null)
            return true;

        var ancestorIds = node
            .Path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse)
            .Where(id => id != nodeId)
            .ToList();

        if (ancestorIds.Count == 0)
            return false;

        return await _ctx.Set<ItemCategoryNode>()
            .AnyAsync(n => ancestorIds.Contains(n.Id) && !n.IsActive, ct);
    }

    public async Task<int> CountItemsByNodeAsync(Guid nodeId, CancellationToken ct = default) =>
        await _ctx.Items.CountAsync(i => i.CategoryNodeId == nodeId && i.IsActive, ct);

    public async Task AddAsync(ItemCategoryNode node, CancellationToken ct = default) =>
        await _ctx.Set<ItemCategoryNode>().AddAsync(node, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _ctx.SaveChangesAsync(ct);
}
