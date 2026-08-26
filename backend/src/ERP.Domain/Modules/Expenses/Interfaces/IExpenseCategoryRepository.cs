using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Domain.Modules.Expenses.Interfaces;

public interface IExpenseCategoryRepository
{
    Task<ExpenseCategoryNode?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ExpenseCategoryNode>> GetChildrenAsync(
        Guid tenantId,
        Guid? parentId,
        bool includeInactive = false,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ExpenseCategoryNode>> GetTreeAsync(
        Guid tenantId,
        bool includeInactive = false,
        CancellationToken ct = default
    );

    Task<bool> HasActiveChildrenAsync(
        Guid tenantId,
        Guid parentId,
        CancellationToken ct = default
    );

    Task<bool> CodeExistsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? parentId,
        ExpenseCategoryNodeLevel level,
        string code,
        Guid? excludeId = null,
        CancellationToken ct = default
    );

    Task<bool> NameExistsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? parentId,
        ExpenseCategoryNodeLevel level,
        string name,
        Guid? excludeId = null,
        CancellationToken ct = default
    );

    Task<bool> IsAccountingAccountUsableForSubcategoryAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountingAccountId,
        CancellationToken ct = default
    );

    Task AddAsync(ExpenseCategoryNode node, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
