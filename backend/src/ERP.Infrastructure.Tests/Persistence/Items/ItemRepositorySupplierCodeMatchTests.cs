using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Items;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Items;

public sealed class ItemRepositorySupplierCodeMatchTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_item_supplier_code_match_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task GetSupplierCodeMatchAsync_devuelve_presentacion_configurada_para_codigo_proveedor()
    {
        Guid supplierId;

        await using (var db = CreateContext(_tenantId))
        {
            var supplier = BusinessPartner.Create(
                _tenantId,
                "04",
                "1791352688001",
                2,
                "Proveedor",
                _createdBy
            );
            supplierId = supplier.Id;

            var itemType = ItemTypeDefinition.Create(_tenantId, "PHYSICAL", "Fisico", 1, _createdBy);
            var item = Item.Create(
                _tenantId,
                "SKU-PACA",
                "Producto PACA",
                "Producto PACA",
                itemType.Id,
                "UNIT",
                ItemTaxConfig.Create("10", "10"),
                ItemSaleConfig.Create(),
                ItemStockConfig.Create(),
                _createdBy
            );
            item.ReplacePackagingLevels(
                [
                    ("Unidad", 1, 1m, "UNIT", null, null, true, false, true),
                    ("Paca 12", 2, 12m, "PACA", null, null, false, true, false),
                ],
                _createdBy
            );
            var packagingId = item.PackagingLevels.Single(x => x.UomCode == "PACA").Id;
            item.AddSupplierCode("PROV-PACA-12", false, supplierId, _createdBy, packagingId);

            db.BusinessPartners.Add(supplier);
            db.ItemTypes.Add(itemType);
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext(_tenantId);
        var repository = new ItemRepository(readDb);

        var match = await repository.GetSupplierCodeMatchAsync(
            supplierId,
            "PROV-PACA-12",
            _tenantId
        );

        match.Should().NotBeNull();
        match!.PackagingLevelId.Should().NotBeNull();
        match.PackagingUomCode.Should().Be("PACA");
        match.PackagingBaseQuantity.Should().Be(12m);
        match.BaseUomCode.Should().Be("UNIT");
    }

    private ErpDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany()
        );
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
