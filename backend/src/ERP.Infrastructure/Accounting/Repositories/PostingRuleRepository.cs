using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class PostingRuleRepository : IPostingRuleRepository
{
    private readonly ErpDbContext _context;

    public PostingRuleRepository(ErpDbContext context)
    {
        _context = context;
    }

    private IQueryable<PostingRule> Scoped(Guid tenantId, Guid companyId)
        => _context.PostingRules.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

    public Task<PostingRule?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default)
        => Scoped(tenantId, companyId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PostingRule>> GetByCompanyAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
        => await Scoped(tenantId, companyId)
            .OrderBy(x => x.SourceModule).ThenBy(x => x.FactType)
            .ToListAsync(ct);

    public Task<PostingRule?> FindByKeyAsync(
        Guid tenantId, Guid companyId, string sourceModule, string factType, CancellationToken ct = default)
        // Include(Lines) — Fase 3.5.5: JournalFactory necesita PostingRule.Lines para construir
        // JournalEntryLine reales; sin este Include, la colección siempre llegaría vacía a
        // PostingRuleResolver (PostingRule no usa lazy loading — sealed, sin navegación virtual).
        => Scoped(tenantId, companyId)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceModule == sourceModule && x.FactType == factType, ct);

    public Task AddAsync(PostingRule rule, CancellationToken ct = default)
        => _context.PostingRules.AddAsync(rule, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
