using ERP.Application.Items.UseCases.AttributeGroups;
using ERP.Application.Items.UseCases.AttributeDefinitions;
using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class AttributeGroupRepository : IAttributeGroupRepository
{
    private readonly ErpDbContext _context;
    public AttributeGroupRepository(ErpDbContext context) => _context = context;

    private IQueryable<AttributeGroup> Scoped(Guid subscriberId)
        => _context.AttributeGroups.Where(x => x.SubscriberId == subscriberId);

    public async Task<IReadOnlyList<AttributeGroup>> GetAllAsync(Guid subscriberId, CancellationToken ct)
        => await Scoped(subscriberId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);

    public async Task<AttributeGroup?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct)
        => await Scoped(subscriberId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ExistsByCodeAsync(string code, Guid subscriberId, CancellationToken ct)
        => await Scoped(subscriberId).AnyAsync(x => x.Code == code.Trim().ToUpperInvariant(), ct);

    public async Task AddAsync(AttributeGroup group, CancellationToken ct)
        => await _context.AttributeGroups.AddAsync(group, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await _context.SaveChangesAsync(ct);
}

public sealed class AttributeDefinitionRepository : IAttributeDefinitionRepository
{
    private readonly ErpDbContext _context;
    public AttributeDefinitionRepository(ErpDbContext context) => _context = context;

    private IQueryable<AttributeDefinition> Scoped(Guid subscriberId)
        => _context.AttributeDefinitions.Where(x => x.SubscriberId == subscriberId);

    public async Task<IReadOnlyList<AttributeDefinition>> GetAllAsync(Guid subscriberId, Guid? groupId, CancellationToken ct)
    {
        var q = Scoped(subscriberId);
        if (groupId.HasValue) q = q.Where(x => x.GroupId == groupId.Value);
        return await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<AttributeDefinition?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct)
        => await Scoped(subscriberId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ExistsByCodeAsync(Guid groupId, string code, Guid subscriberId, CancellationToken ct)
        => await Scoped(subscriberId).AnyAsync(x => x.GroupId == groupId && x.Code == code.Trim().ToUpperInvariant(), ct);

    public async Task AddAsync(AttributeDefinition def, CancellationToken ct)
        => await _context.AttributeDefinitions.AddAsync(def, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await _context.SaveChangesAsync(ct);
}
