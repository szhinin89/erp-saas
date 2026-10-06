using ERP.Domain.Modules.Inventory.Policies;
using FluentAssertions;

namespace ERP.Domain.Tests.Inventory;

public sealed class SaleStockPolicyTests
{
    public static IEnumerable<object[]> Matrix()
    {
        foreach (var product in new[] { false, true })
        foreach (var company in new[] { false, true })
        foreach (var item in new[] { false, true })
        foreach (var allow in new[] { false, true })
            yield return new object[] { product, company, item, allow, product && company && item && !allow };
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Resolves_approved_availability_matrix(bool product, bool company, bool item, bool allow, bool expected)
    {
        SaleStockPolicy.RequiresAvailableStock(product, company, item, allow).Should().Be(expected);
    }
}
