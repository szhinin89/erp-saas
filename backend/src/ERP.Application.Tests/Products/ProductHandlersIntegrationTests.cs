using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Application.Products.UseCases.GetProducts;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Tests.Products;

public class ProductHandlersIntegrationTests
{
    [Fact]
    public async Task CreateProductHandler_should_create_product_for_current_tenant()
    {
        var subscriberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productRepo = new InMemoryProductRepository();
        var taxRepo = new InMemoryTaxRateRepository();
        var activityRepo = new InMemoryUserActivityRepository();
        var currentSubscriber = new TestCurrentSubscriber(subscriberId);
        var currentCompany = new TestCurrentCompany(Guid.NewGuid());
        var currentUser = new TestCurrentUser(userId);

        var handler = new CreateProductCommandHandler(
            productRepo,
            taxRepo,
            activityRepo,
            currentSubscriber,
            currentCompany,
            currentUser);

        var command = new CreateProductCommand(
            SaleCode: "P-001",
            ShortName: "Producto test",
            Description: "Descripción test",
            LineId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            SubcategoryId: Guid.NewGuid(),
            UnitOfMeasureId: Guid.NewGuid(),
            BrandId: Guid.NewGuid(),
            ProductTypeId: Guid.NewGuid(),
            TariffId: Guid.NewGuid(),
            AppliesVatOnSale: false,
            SaleTaxId: null,
            SaleVatAccountId: null,
            AppliesVatOnPurchase: false,
            PurchaseTaxId: null,
            PurchaseVatAccountId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SaleCode.Should().Be("P-001");

        var persisted = await productRepo.GetAllBySubscriberAsync(subscriberId, CancellationToken.None);
        persisted.Should().HaveCount(1);
        persisted[0].SubscriberId.Should().Be(subscriberId);
        persisted[0].CreatedBy.Should().Be(userId);
        activityRepo.Activities.Should().ContainSingle();
    }

    [Fact]
    public async Task GetProductsHandler_should_return_only_current_tenant_products()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repo = new InMemoryProductRepository();

        await repo.AddAsync(BuildProduct(tenantA, userId, "A-001"), CancellationToken.None);
        await repo.AddAsync(BuildProduct(tenantB, userId, "B-001"), CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        var handler = new GetProductsHandler(repo, new TestCurrentSubscriber(tenantA));
        var result = await handler.Handle(new GetProductsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(1);
        result.Value[0].SaleCode.Should().Be("A-001");
    }

    private static Product BuildProduct(Guid subscriberId, Guid userId, string saleCode)
    {
        return Product.Create(
            subscriberId: subscriberId,
            saleCode: saleCode,
            shortName: $"Producto {saleCode}",
            description: "Producto de integración",
            lineId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            subcategoryId: Guid.NewGuid(),
            unitOfMeasureId: Guid.NewGuid(),
            brandId: Guid.NewGuid(),
            productTypeId: Guid.NewGuid(),
            tariffId: Guid.NewGuid(),
            appliesVatOnSale: false,
            saleTaxId: null,
            saleVatAccountId: null,
            appliesVatOnPurchase: false,
            purchaseTaxId: null,
            purchaseVatAccountId: null,
            createdBy: userId);
    }

    private sealed class TestCurrentSubscriber : ICurrentSubscriber
    {
        public TestCurrentSubscriber(Guid subscriberId) => SubscriberId = subscriberId;
        public Guid SubscriberId { get; }
        public bool IsAuthenticated => true;
    }

    private sealed class TestCurrentCompany : ICurrentCompany
    {
        public TestCurrentCompany(Guid companyId) => CompanyId = companyId;
        public Guid CompanyId { get; }
        public bool HasCompanyContext => true;
        public bool IsAuthenticated => true;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId)
        {
            UserId = userId;
            Email = "integration@test.local";
            FullName = "Integration Test";
        }

        public Guid UserId { get; }
        public string? Email { get; }
        public string? FullName { get; }
        public string? Role => "Admin";
        public bool IsAuthenticated => true;
    }

    private sealed class InMemoryTaxRateRepository : ITaxRateRepository
    {
        public Task<TaxRate?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
            => Task.FromResult<TaxRate?>(null);
    }

    private sealed class InMemoryUserActivityRepository : IUserActivityRepository
    {
        public List<UserActivity> Activities { get; } = new();

        public Task AddAsync(UserActivity activity, CancellationToken ct = default)
        {
            Activities.Add(activity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserActivity>> GetMyRecentAsync(
            Guid subscriberId,
            Guid userId,
            string? module = null,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<UserActivity>>(Activities);
        }

        public Task<IReadOnlyList<UserActivity>> GetByEntityAsync(
            Guid subscriberId,
            string entityType,
            Guid entityId,
            int take = 10,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<UserActivity>>(Activities);
        }
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products = new();

        public Task<Product?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
            => Task.FromResult(_products.FirstOrDefault(p => p.Id == id && p.SubscriberId == subscriberId));

        public Task<Product?> GetByIdWithDetailsAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
            => Task.FromResult(_products.FirstOrDefault(p => p.Id == id && p.SubscriberId == subscriberId));

        public Task<IReadOnlyList<Product>> GetAllBySubscriberAsync(Guid subscriberId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Product>>(
                _products.Where(p => p.SubscriberId == subscriberId && p.IsActive).ToList());

        public Task<IReadOnlyList<Product>> GetReportAsync(Guid subscriberId, ProductReportFilter filter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Product>>(
                _products.Where(p => p.SubscriberId == subscriberId).ToList());

        public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetReportPageAsync(
            Guid subscriberId,
            ProductReportFilter filter,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            var items = _products.Where(p => p.SubscriberId == subscriberId).ToList();
            return Task.FromResult<(IReadOnlyList<Product>, int)>((items, items.Count));
        }

        public Task AddAsync(Product product, CancellationToken ct = default)
        {
            _products.Add(product);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Product product, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
