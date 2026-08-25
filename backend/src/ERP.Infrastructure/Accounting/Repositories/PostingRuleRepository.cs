using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class PostingRuleRepository : IPostingRuleRepository
{
    private readonly ErpDbContext _context;

    public PostingRuleRepository(ErpDbContext context)
    {
        _context = context;
    }

    private IQueryable<PostingRule> Scoped(Guid tenantId, Guid companyId) =>
        _context.PostingRules.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

    // ACCOUNTING-POSTING-RULES-UI-FIX-12B: GetByIdAsync/GetByCompanyAsync no traían
    // .Include(x => x.Lines) — mismo motivo ya documentado en FindByKeyAsync más abajo (PostingRule
    // no usa lazy loading, sealed, sin navegación virtual), pero nadie lo había notado porque hasta
    // ACCOUNTING-POSTING-RULES-UI-12 ningún consumidor real de estos dos métodos leía Lines (los
    // handlers de administración solo tocaban DebitAccountId/CreditAccountId legacy). La pantalla
    // de Reglas contables sí depende de Lines — sin este fix, GetPostingRulesQuery devolvía las 7
    // reglas con `lines: []` cada una (activas, con SourceModule/FactType correctos), así que la
    // pantalla no tenía nada real que mostrar en Debe/Haber aunque los 21/63 registros ya existían
    // en `posting_rules`/`posting_rule_lines`.
    public Task<PostingRule?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    ) => Scoped(tenantId, companyId).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PostingRule>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    ) =>
        await Scoped(tenantId, companyId)
            .Include(x => x.Lines)
            .OrderBy(x => x.SourceModule)
            .ThenBy(x => x.FactType)
            .ToListAsync(ct);

    public Task<PostingRule?> FindByKeyAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        CancellationToken ct = default
    )
        // Include(Lines) — Fase 3.5.5: JournalFactory necesita PostingRule.Lines para construir
        // JournalEntryLine reales; sin este Include, la colección siempre llegaría vacía a
        // PostingRuleResolver (PostingRule no usa lazy loading — sealed, sin navegación virtual).
        =>
        Scoped(tenantId, companyId)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceModule == sourceModule && x.FactType == factType, ct);

    public Task AddAsync(PostingRule rule, CancellationToken ct = default) =>
        _context.PostingRules.AddAsync(rule, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
