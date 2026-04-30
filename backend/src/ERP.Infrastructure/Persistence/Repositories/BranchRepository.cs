using Microsoft.EntityFrameworkCore;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository : IBranchRepository
{
    private readonly ErpDbContext _context;

    public BranchRepository(ErpDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(Branch branch, CancellationToken ct = default)
        => _context.Branches.AddAsync(branch, ct).AsTask();

    public async Task<IReadOnlyList<Branch>> GetAsync(
        Guid tenantId,
        bool? activeFilter = true,
        string? search = null,
        CancellationToken ct = default)
    {
        var q = _context.Branches.AsQueryable().Where(x => x.TenantId == tenantId);
        if (activeFilter is true)
            q = q.Where(x => x.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.Name.ToLower().Contains(s) ||
                x.Address.ToLower().Contains(s) ||
                (x.Phones != null && x.Phones.ToLower().Contains(s)));
        }

        return await q.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public Task<Branch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.Branches.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public async Task ClearMainBranchExceptAsync(Guid tenantId, Guid? exceptBranchId, Guid updatedBy, CancellationToken ct = default)
    {
        var q = _context.Branches.Where(b => b.TenantId == tenantId && b.IsMainBranch);
        if (exceptBranchId is not null)
            q = q.Where(b => b.Id != exceptBranchId.Value);

        var list = await q.ToListAsync(ct);
        foreach (var b in list)
            b.SetMainBranchFlag(false, updatedBy);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
