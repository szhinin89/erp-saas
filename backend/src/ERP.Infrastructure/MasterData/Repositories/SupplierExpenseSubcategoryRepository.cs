using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Models;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio del agregado SupplierExpenseSubcategory — EXPENSE-SUPPLIER-SUBCATEGORIES-01.
/// Los query filters globales (tenant + empresa, fail-closed) aplican tanto a las asignaciones como
/// a <c>expense_category_nodes</c>: un nodo de otra empresa no es visible y no se devuelve.
/// </summary>
public sealed class SupplierExpenseSubcategoryRepository : ISupplierExpenseSubcategoryRepository
{
    private readonly ErpDbContext _db;

    public SupplierExpenseSubcategoryRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SupplierExpenseSubcategory>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SupplierExpenseSubcategories.Where(x => x.BusinessPartnerId == businessPartnerId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExpenseSubcategoryInfo>> GetSubcategoryInfosAsync(
        IReadOnlyCollection<Guid> nodeIds,
        CancellationToken cancellationToken = default
    )
    {
        if (nodeIds.Count == 0)
            return [];

        var nodes = await _db
            .ExpenseCategoryNodes.AsNoTracking()
            .Where(n => nodeIds.Contains(n.Id))
            .Select(n => new
            {
                n.Id,
                n.ParentId,
                n.Code,
                n.Name,
                n.Level,
                n.IsActive,
            })
            .ToListAsync(cancellationToken);

        var categoryIds = nodes.Where(n => n.ParentId.HasValue).Select(n => n.ParentId!.Value).Distinct().ToList();
        var categories = await _db
            .ExpenseCategoryNodes.AsNoTracking()
            .Where(n => categoryIds.Contains(n.Id))
            .Select(n => new { n.Id, n.ParentId, n.Name })
            .ToListAsync(cancellationToken);

        var typeIds = categories.Where(c => c.ParentId.HasValue).Select(c => c.ParentId!.Value).Distinct().ToList();
        var types = await _db
            .ExpenseCategoryNodes.AsNoTracking()
            .Where(n => typeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name })
            .ToDictionaryAsync(n => n.Id, n => n.Name, cancellationToken);
        var categoryById = categories.ToDictionary(c => c.Id);

        return nodes
            .Select(n =>
            {
                var category = n.ParentId.HasValue && categoryById.TryGetValue(n.ParentId.Value, out var c) ? c : null;
                var typeName =
                    category?.ParentId is { } typeId && types.TryGetValue(typeId, out var t)
                        ? t
                        : string.Empty;
                return new ExpenseSubcategoryInfo(
                    n.Id,
                    n.Code,
                    n.Name,
                    category?.Name ?? string.Empty,
                    typeName,
                    n.IsActive,
                    n.Level == ExpenseCategoryNodeLevel.Subcategory
                );
            })
            .ToList();
    }

    public async Task AddAsync(
        SupplierExpenseSubcategory assignment,
        CancellationToken cancellationToken = default
    ) => await _db.SupplierExpenseSubcategories.AddAsync(assignment, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
