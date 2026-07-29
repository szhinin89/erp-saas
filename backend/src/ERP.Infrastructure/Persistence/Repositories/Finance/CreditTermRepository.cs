using ERP.Application.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Finance;

public sealed class CreditTermRepository : ICreditTermRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public CreditTermRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<CreditTerm> Scoped(Guid tenantId) =>
        _context.CreditTerms.Include(t => t.Installments).ForOperationalScope(tenantId, _company);

    public Task<CreditTerm?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        Scoped(tenantId).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<CreditTerm>> GetAllAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId);

        if (activeFilter is true)
            q = q.Where(t => t.IsActive);
        else if (activeFilter is false)
            q = q.Where(t => !t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(t => t.Name.Contains(s) || t.Code.Contains(s));
        }

        return await q.OrderBy(t => t.Code).ToListAsync(ct);
    }

    public async Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? excludeId,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId).Where(t => t.Code == code.Trim().ToUpperInvariant());
        if (excludeId.HasValue)
            q = q.Where(t => t.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public Task AddAsync(CreditTerm term, CancellationToken ct = default) =>
        _context.CreditTerms.AddAsync(term, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
