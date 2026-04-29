using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Tests;

public class ErpDbContextTenantFilterTests
{
    private sealed class FakeTenant : ICurrentTenant
    {
        public Guid TenantId { get; init; }
        public bool IsAuthenticated { get; init; } = true;
    }

    [Fact]
    public async Task QueryFilter_should_return_only_current_tenant_entities()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;

        await using (var seed = new ErpDbContext(options, new FakeTenant { TenantId = tenantA }))
        {
            seed.Products.Add(Product.Create(
                tenantA,
                saleCode: "A",
                shortName: "A",
                description: "A",
                lineId: Guid.NewGuid(),
                categoryId: Guid.NewGuid(),
                subcategoryId: Guid.NewGuid(),
                unitOfMeasureId: Guid.NewGuid(),
                brandId: Guid.NewGuid(),
                productTypeId: Guid.NewGuid(),
                tariffId: Guid.NewGuid(),
                saleTaxId: Guid.NewGuid(),
                purchaseTaxId: Guid.NewGuid(),
                createdBy: tenantA));

            seed.Products.Add(Product.Create(
                tenantB,
                saleCode: "B",
                shortName: "B",
                description: "B",
                lineId: Guid.NewGuid(),
                categoryId: Guid.NewGuid(),
                subcategoryId: Guid.NewGuid(),
                unitOfMeasureId: Guid.NewGuid(),
                brandId: Guid.NewGuid(),
                productTypeId: Guid.NewGuid(),
                tariffId: Guid.NewGuid(),
                saleTaxId: Guid.NewGuid(),
                purchaseTaxId: Guid.NewGuid(),
                createdBy: tenantB));

            await seed.SaveChangesAsync();
        }

        await using var ctxA = new ErpDbContext(options, new FakeTenant { TenantId = tenantA });
        var products = await ctxA.Products.ToListAsync();

        products.Should().HaveCount(1);
        products[0].SaleCode.Should().Be("A");
    }
}
