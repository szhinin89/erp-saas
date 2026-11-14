using ERP.Application.MasterData.Services;
using Moq;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01 — dobles de <see cref="ISupplierExpenseSubcategoryPolicy"/>
/// para los handlers de Gastos: por defecto el proveedor está sin configuración (catálogo completo).
/// </summary>
internal static class SupplierExpenseSubcategoryTestDoubles
{
    internal static Mock<ISupplierExpenseSubcategoryPolicy> Unconfigured() =>
        WithScope(
            new SupplierExpenseSubcategoryScope(
                SupplierExpenseSubcategoryMode.Unconfigured,
                new HashSet<Guid>()
            )
        );

    internal static Mock<ISupplierExpenseSubcategoryPolicy> Restricted(params Guid[] available) =>
        WithScope(
            new SupplierExpenseSubcategoryScope(
                SupplierExpenseSubcategoryMode.Restricted,
                available.ToHashSet()
            )
        );

    internal static Mock<ISupplierExpenseSubcategoryPolicy> RestrictedEmpty() =>
        WithScope(
            new SupplierExpenseSubcategoryScope(
                SupplierExpenseSubcategoryMode.RestrictedEmpty,
                new HashSet<Guid>()
            )
        );

    private static Mock<ISupplierExpenseSubcategoryPolicy> WithScope(
        SupplierExpenseSubcategoryScope scope
    )
    {
        var mock = new Mock<ISupplierExpenseSubcategoryPolicy>();
        mock.Setup(p => p.GetScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scope);
        return mock;
    }
}
