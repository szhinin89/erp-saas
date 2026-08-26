using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Expenses;

public sealed class ExpenseCategoryRepository : IExpenseCategoryRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public ExpenseCategoryRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<ExpenseCategoryNode> Scoped(Guid tenantId) =>
        _db.ExpenseCategoryNodes.ForOperationalScope(tenantId, _company);

    public Task<ExpenseCategoryNode?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) => Scoped(tenantId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<ExpenseCategoryNode>> GetChildrenAsync(
        Guid tenantId,
        Guid? parentId,
        bool includeInactive = false,
        CancellationToken ct = default
    )
    {
        var query = Scoped(tenantId).Where(x => x.ParentId == parentId);
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        return await query.OrderBy(x => x.Code).ThenBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ExpenseCategoryNode>> GetTreeAsync(
        Guid tenantId,
        bool includeInactive = false,
        CancellationToken ct = default
    )
    {
        var query = Scoped(tenantId);
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        return await query
            .OrderBy(x => x.Level)
            .ThenBy(x => x.ParentId)
            .ThenBy(x => x.Code)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public Task<bool> HasActiveChildrenAsync(
        Guid tenantId,
        Guid parentId,
        CancellationToken ct = default
    ) => Scoped(tenantId).AnyAsync(x => x.ParentId == parentId && x.IsActive, ct);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? parentId,
        ExpenseCategoryNodeLevel level,
        string code,
        Guid? excludeId = null,
        CancellationToken ct = default
    )
    {
        var normalized = code.Trim().ToUpperInvariant();
        var query = _db.ExpenseCategoryNodes.Where(x =>
            x.TenantId == tenantId
            && x.CompanyId == companyId
            && x.ParentId == parentId
            && x.Level == level
            && x.Code == normalized
        );
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return query.AnyAsync(ct);
    }

    public Task<bool> NameExistsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? parentId,
        ExpenseCategoryNodeLevel level,
        string name,
        Guid? excludeId = null,
        CancellationToken ct = default
    )
    {
        var normalized = name.Trim();
        var query = _db.ExpenseCategoryNodes.Where(x =>
            x.TenantId == tenantId
            && x.CompanyId == companyId
            && x.ParentId == parentId
            && x.Level == level
            && x.Name == normalized
        );
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return query.AnyAsync(ct);
    }

    public Task<bool> IsAccountingAccountUsableForSubcategoryAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountingAccountId,
        CancellationToken ct = default
    ) =>
        _db.Accounts.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.Id == accountingAccountId
                && x.IsActive
                && x.AllowsPosting
                && x.AccountType == AccountType.Expense,
            ct
        );

    public Task AddAsync(ExpenseCategoryNode node, CancellationToken ct = default) =>
        _db.ExpenseCategoryNodes.AddAsync(node, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
