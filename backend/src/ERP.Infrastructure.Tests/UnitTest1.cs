using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;
using MediatR;

namespace ERP.Infrastructure.Tests;

public class ErpDbContextTenantFilterTests
{
    private sealed class FakeSubscriber : ICurrentSubscriber
    {
        public Guid SubscriberId { get; init; }
        public bool IsAuthenticated { get; init; } = true;
    }

    private sealed class FakePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    [Fact]
    public async Task QueryFilter_should_return_only_current_tenant_entities()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;

        var publisher = new FakePublisher();
        await using (var seed = TestErpDbContextFactory.Create(options, new FakeSubscriber { SubscriberId = tenantA }, publisher))
        {
            seed.Products.Add(Product.Create(
                tenantA,
                saleCode: "A",
                shortName: "A",
                description: "A",
                lineId: Guid.NewGuid(),
                categoryId: Guid.NewGuid(),
                subcategoryId: Guid.NewGuid(),
                uomCode: "19",
                brandId: Guid.NewGuid(),
                productTypeId: Guid.NewGuid(),
                tariffId: Guid.NewGuid(),
                appliesVatOnSale: true,
                saleVatCode: "10",
                saleVatAccountId: null,
                appliesVatOnPurchase: true,
                purchaseVatCode: "10",
                purchaseVatAccountId: null,
                createdBy: tenantA));

            seed.Products.Add(Product.Create(
                tenantB,
                saleCode: "B",
                shortName: "B",
                description: "B",
                lineId: Guid.NewGuid(),
                categoryId: Guid.NewGuid(),
                subcategoryId: Guid.NewGuid(),
                uomCode: "19",
                brandId: Guid.NewGuid(),
                productTypeId: Guid.NewGuid(),
                tariffId: Guid.NewGuid(),
                appliesVatOnSale: true,
                saleVatCode: "10",
                saleVatAccountId: null,
                appliesVatOnPurchase: true,
                purchaseVatCode: "10",
                purchaseVatAccountId: null,
                createdBy: tenantB));

            await seed.SaveChangesAsync();
        }

        await using var ctxA = TestErpDbContextFactory.Create(options, new FakeSubscriber { SubscriberId = tenantA }, publisher);
        var products = await ctxA.Products.ToListAsync();

        products.Should().HaveCount(1);
        products[0].SaleCode.Should().Be("A");
    }
}
