using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.MasterData.Services;

public sealed class SupplierExpenseSubcategoryPolicy : ISupplierExpenseSubcategoryPolicy
{
    private readonly ISupplierExpenseSubcategoryRepository _repo;

    public SupplierExpenseSubcategoryPolicy(ISupplierExpenseSubcategoryRepository repo) =>
        _repo = repo;

    public async Task<SupplierExpenseSubcategoryScope> GetScopeAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default
    )
    {
        var assignments = await _repo.GetByBusinessPartnerAsync(supplierId, cancellationToken);
        var activeIds = assignments.Where(x => x.IsActive).Select(x => x.ExpenseSubcategoryId).ToList();
        if (activeIds.Count == 0)
            return SupplierExpenseSubcategoryScope.From(assignments, []);

        var infos = await _repo.GetSubcategoryInfosAsync(activeIds, cancellationToken);
        return SupplierExpenseSubcategoryScope.From(assignments, infos);
    }
}
