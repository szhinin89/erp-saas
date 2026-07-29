using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository : IBranchRepository
{
    private readonly ErpDbContext _context;

    public BranchRepository(ErpDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(Branch branch, CancellationToken cancellationToken = default)
        => _context.Branches.AddAsync(branch, cancellationToken).AsTask();

    public async Task<IReadOnlyList<Branch>> GetAsync(
        Guid tenantId,
        bool? activeFilter = true,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var q = _context.Branches.AsQueryable().Where(x => x.TenantId == tenantId);
        if (activeFilter is true)
            q = q.Where(x => x.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Name, pattern) ||
                EF.Functions.ILike(x.Address, pattern) ||
                (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)));
        }

        return await q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Branch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => _context.Branches.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task ClearMainBranchExceptAsync(Guid tenantId, Guid? exceptBranchId, Guid updatedBy, CancellationToken cancellationToken = default)
    {
        var q = _context.Branches.Where(b => b.TenantId == tenantId && b.IsMainBranch);
        if (exceptBranchId is not null)
            q = q.Where(b => b.Id != exceptBranchId.Value);

        var list = await q.ToListAsync(cancellationToken);
        foreach (var b in list)
            b.SetMainBranchFlag(false, updatedBy);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
