using ERP.Application.Items.UseCases.AttributeDefinitions;
using ERP.Application.Items.UseCases.AttributeGroups;
using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class AttributeGroupRepository : IAttributeGroupRepository
{
    private readonly ErpDbContext _context;

    public AttributeGroupRepository(ErpDbContext context) => _context = context;

    private IQueryable<AttributeGroup> Scoped(Guid tenantId) =>
        _context.AttributeGroups.Where(x => x.TenantId == tenantId);

    public async Task<IReadOnlyList<AttributeGroup>> GetAllAsync(
        Guid tenantId,
        CancellationToken cancellationToken
    ) =>
        await Scoped(tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<AttributeGroup?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken
    ) => await Scoped(tenantId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> ExistsByCodeAsync(
        string code,
        Guid tenantId,
        CancellationToken cancellationToken
    )
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await Scoped(tenantId).AnyAsync(x => x.Code == normalized, cancellationToken);
    }

    public async Task AddAsync(AttributeGroup group, CancellationToken cancellationToken) =>
        await _context.AttributeGroups.AddAsync(group, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}

public sealed class AttributeDefinitionRepository : IAttributeDefinitionRepository
{
    private readonly ErpDbContext _context;

    public AttributeDefinitionRepository(ErpDbContext context) => _context = context;

    private IQueryable<AttributeDefinition> Scoped(Guid tenantId) =>
        _context.AttributeDefinitions.Where(x => x.TenantId == tenantId);

    public async Task<IReadOnlyList<AttributeDefinition>> GetAllAsync(
        Guid tenantId,
        Guid? groupId,
        CancellationToken cancellationToken
    )
    {
        var q = Scoped(tenantId);
        if (groupId.HasValue)
            q = q.Where(x => x.GroupId == groupId.Value);
        return await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<AttributeDefinition?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken
    ) => await Scoped(tenantId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> ExistsByCodeAsync(
        Guid groupId,
        string code,
        Guid tenantId,
        CancellationToken cancellationToken
    )
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await Scoped(tenantId)
            .AnyAsync(x => x.GroupId == groupId && x.Code == normalized, cancellationToken);
    }

    public async Task AddAsync(AttributeDefinition def, CancellationToken cancellationToken) =>
        await _context.AttributeDefinitions.AddAsync(def, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}
