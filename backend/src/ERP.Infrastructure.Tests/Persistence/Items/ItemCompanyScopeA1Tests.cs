using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Items;

/// <summary>A1 security and uniqueness tests run against actual PostgreSQL and the complete migration chain.</summary>
public sealed class ItemCompanyScopeA1Tests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();
    private Guid _tenantId;
    private Guid _companyA;
    private Guid _companyB;
    private Guid _typeId;
    private Guid _supplierId;
    private Guid _warehouse1;
    private Guid _warehouse2;
    private readonly Guid _actor = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Context(Guid.Empty);
        await db.Database.MigrateAsync();
        var tenant = Tenant.Create("A1 Tenant", $"a1-{Guid.NewGuid():N}"[..16], _actor);
        var companyA = Company.CreateManaged(tenant.Id, "1790012345001", "A1 Company A", createdBy: _actor);
        var companyB = Company.CreateManaged(tenant.Id, "1790012345002", "A1 Company B", createdBy: _actor);
        _tenantId = tenant.Id;
        _companyA = companyA.Id;
        _companyB = companyB.Id;
        var type = ItemTypeDefinition.Create(_tenantId, "CLASS", "Descriptive classification", 0, _actor);
        _typeId = type.Id;
        var supplier = BusinessPartner.Create(_tenantId, "04", "1791352688001", 2, "Tenant Supplier", _actor);
        _supplierId = supplier.Id;
        var branch = Branch.Create(_tenantId, "A1 Branch", "Address", "A1", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, _actor, companyId: _companyA);
        var warehouse1 = Warehouse.Create(_tenantId, branch.Id, "Warehouse 1", "W1", null, null, null, null, null, null, null, null, null, _actor, _companyA);
        var warehouse2 = Warehouse.Create(_tenantId, branch.Id, "Warehouse 2", "W2", null, null, null, null, null, null, null, null, null, _actor, _companyA);
        _warehouse1 = warehouse1.Id;
        _warehouse2 = warehouse2.Id;
        db.Tenants.Add(tenant);
        db.Companies.AddRange(companyA, companyB);
        db.Branches.Add(branch);
        db.Warehouses.AddRange(warehouse1, warehouse2);
        db.ItemTypes.Add(type);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext Context(Guid companyId) => new(
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
        new CurrentTenant(_tenantId), new NoOpPublisher(), new CurrentCompany(companyId));

    private Item Product(Guid company, string sku, bool control = true, ItemNature nature = ItemNature.Product) =>
        Item.Create(_tenantId, sku, sku, sku, _typeId, "UNIT", ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(),
            ItemStockConfig.Create(stockControlEnabled: control), _actor, companyId: company, nature: nature);

    private async Task<Item> Save(Guid company, string sku, string? barcode = null, string? variantSku = null, string? supplierCode = null)
    {
        await using var db = Context(company);
        var item = Product(company, sku);
        var variant = item.AddVariant([], variantSku ?? sku, 0, _actor);
        if (barcode is not null) variant.AddBarcode(barcode, "Internal", _tenantId, _actor);
        if (supplierCode is not null) item.AddSupplierCode(supplierCode, false, _supplierId, _actor);
        var repo = new ItemRepository(db);
        await repo.AddAsync(item);
        await repo.SaveChangesAsync();
        return item;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BarcodeExists_A5_respects_explicit_company_and_tenant(bool packaging)
    {
        const string code = "A5-BAR";
        await using (var owner = Context(_companyA))
        {
            var item = Product(_companyA, "A5-OWNER");
            if (packaging)
                item.ReplacePackagingLevels([("UNIT", 1, 1m, "UNIT", code, null, true, true, true)], _actor);
            else
                item.AddVariant([], "A5-VAR", 0, _actor).AddBarcode(code, "Internal", _tenantId, _actor);
            owner.Items.Add(item);
            await owner.SaveChangesAsync();
        }
        await using var db = Context(_companyA);
        var repo = new ItemRepository(db);
        (await repo.BarcodeExistsAsync(code, _tenantId, _companyA)).Should().BeTrue();
        Func<Task> duplicate = () => Save(_companyA, "A5-DUPLICATE", code);
        await duplicate.Should().ThrowAsync<DbUpdateException>();
        (await repo.BarcodeExistsAsync(code, _tenantId, _companyB)).Should().BeFalse();
        (await repo.BarcodeExistsAsync(code, Guid.NewGuid(), _companyA)).Should().BeFalse();
        (await repo.BarcodeExistsAsync(code, _tenantId, Guid.Empty)).Should().BeFalse();
        await using var otherCompany = Context(_companyB);
        (await new ItemRepository(otherCompany).BarcodeExistsAsync(code, _tenantId, _companyB)).Should().BeFalse();
        await Save(_companyB, "A5-OTHER", code);
        (await new ItemRepository(otherCompany).BarcodeExistsAsync(code, _tenantId, _companyB)).Should().BeTrue();
        var otherTenant = Tenant.Create("A5 Other Tenant", $"a5-{Guid.NewGuid():N}"[..16], _actor);
        var company = Company.CreateManaged(otherTenant.Id, "1790012345003", "A5 Other Company", createdBy: _actor);
        var type = ItemTypeDefinition.Create(otherTenant.Id, "A5-CLASS", "Classification", 0, _actor);
        await using var foreign = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new CurrentTenant(otherTenant.Id), new NoOpPublisher(), new CurrentCompany(company.Id));
        foreign.Tenants.Add(otherTenant);
        foreign.Companies.Add(company);
        foreign.ItemTypes.Add(type);
        var foreignItem = Item.Create(otherTenant.Id, "A5-FOREIGN", "Foreign", "Foreign", type.Id, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(), _actor, companyId: company.Id);
        if (packaging)
            foreignItem.ReplacePackagingLevels([("UNIT", 1, 1m, "UNIT", code, null, true, true, true)], _actor);
        else
            foreignItem.AddVariant([], "A5-FOREIGN-VAR", 0, _actor).AddBarcode(code, "Internal", otherTenant.Id, _actor);
        foreign.Items.Add(foreignItem);
        await foreign.SaveChangesAsync();
        var foreignRepo = new ItemRepository(foreign);
        (await foreignRepo.BarcodeExistsAsync(code, otherTenant.Id, company.Id)).Should().BeTrue();
        (await foreignRepo.BarcodeExistsAsync(code, _tenantId, company.Id)).Should().BeFalse();
        (await repo.BarcodeExistsAsync(code, otherTenant.Id, company.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Same_sku_barcode_variant_and_supplier_code_are_allowed_in_another_company()
    {
        var a = await Save(_companyA, "SAME", "BAR", "VAR", "SUP");
        var b = await Save(_companyB, "SAME", "BAR", "VAR", "SUP");
        a.Id.Should().NotBe(b.Id);
        await using var db = Context(_companyB);
        var repo = new ItemRepository(db);
        (await repo.ResolveByAnyCodeAsync("BAR", _tenantId))!.Id.Should().Be(b.Id);
        (await repo.GetSupplierCodeMatchAsync(_supplierId, "SUP", _tenantId))!.ItemId.Should().Be(b.Id);
    }

    [Theory]
    [InlineData("sku")]
    [InlineData("barcode")]
    [InlineData("variant")]
    [InlineData("supplier")]
    public async Task Duplicates_inside_same_company_are_rejected_by_postgresql(string kind)
    {
        await Save(_companyA, "FIRST", "BAR", "VAR", "SUP");
        Func<Task> duplicate = () => Save(_companyA, kind == "sku" ? "FIRST" : "SECOND", kind == "barcode" ? "BAR" : null,
            kind == "variant" ? "VAR" : "SECOND", kind == "supplier" ? "SUP" : null);
        await duplicate.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Foreign_company_item_and_all_children_are_hidden_and_cannot_be_replaced()
    {
        var a = Product(_companyA, "FOREIGN");
        a.AddVariant([], "FOREIGN-VAR", 0, _actor).AddBarcode("FOREIGN-BAR", "Internal", _tenantId, _actor);
        a.AddSupplierCode("FOREIGN-SUP", false, _supplierId, _actor);
        var substitute = await Save(_companyA, "SUBSTITUTE");
        await using (var owner = Context(_companyA))
        {
            var item = a;
            var group = AttributeGroup.Create(_tenantId, "AXIS", "Axis");
            var attribute = AttributeDefinition.Create(_tenantId, group.Id, "SIZE", "Size", ERP.Domain.Modules.Items.Enums.AttributeDataType.Text, isVariantAxis: true);
            owner.Set<AttributeGroup>().Add(group);
            owner.Set<AttributeDefinition>().Add(attribute);
            item.AddVariant([(attribute.Id, "L")], "FOREIGN-L", 1, _actor);
            item.ReplaceImages([(Guid.NewGuid(), "Image", true, true, 0)], _actor);
            item.ReplaceUnitConversions([("BOX", "UNIT", 12m)], _actor);
            item.ReplaceSubstitutes([(substitute.Id, 1, null)], _actor);
            item.ReplacePackagingLevels([("UNIT", 1, 1m, "UNIT", "PACK-BAR", null, true, true, true)], _actor);
            item.ReplaceSpecialTaxConfigurations([("3", "3072")], _actor);
            await new ItemRepository(owner).AddAsync(item);
            owner.Set<ItemAudit>().Add(ItemAudit.Create(new ERP.Domain.Audit.AuditActor(_tenantId, _actor, "Tester", null, null, null, null, null, ERP.Domain.Audit.AuditSource.UserAction), item.Id, "Created"));
            await owner.SaveChangesAsync();
            (await owner.Set<ItemVariantAttribute>().CountAsync()).Should().Be(1);
            (await owner.Set<ItemImage>().CountAsync()).Should().Be(1);
        }
        await using var db = Context(_companyB);
        var repo = new ItemRepository(db);
        (await repo.GetByIdAsync(a.Id, _tenantId)).Should().BeNull();
        (await repo.GetByIdLightAsync(a.Id, _tenantId)).Should().BeNull();
        (await repo.GetBySkuAsync("FOREIGN", _tenantId)).Should().BeNull();
        (await repo.ResolveByAnyCodeAsync("FOREIGN-BAR", _tenantId)).Should().BeNull();
        (await repo.GetPageAsync(_tenantId, new ItemReportFilter(), 1, 20)).Items.Should().BeEmpty();
        (await repo.SearchBySimilarityAsync("FOREIGN", _tenantId, 20, 0)).Should().BeEmpty();
        (await repo.GetSupplierCodeMatchAsync(_supplierId, "FOREIGN-SUP", _tenantId)).Should().BeNull();
        (await db.ItemVariants.ToListAsync()).Should().BeEmpty();
        (await db.ItemVariantBarcodes.ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemSupplierCode>().ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemVariantAttribute>().ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemImage>().ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemUnitConversion>().ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemSubstitute>().ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemPackagingLevel>().ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemSpecialTaxConfiguration>().ToListAsync()).Should().BeEmpty();
        (await db.Set<ItemAudit>().ToListAsync()).Should().BeEmpty();
        Func<Task> replace = () => repo.ReplaceImagesAsync(a.Id, []);
        await replace.Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>();
        var search = new InvoiceItemSearchRepository(db);
        (await search.SearchAsync(_tenantId, _companyB, "FOREIGN", null, 20)).Should().BeEmpty();
    }

    [Fact]
    public async Task Adding_variant_to_existing_item_preserves_company_keys()
    {
        var saved = await Save(_companyA, "VARIANT-OWNER");
        await using var db = Context(_companyA);
        var repo = new ItemRepository(db);
        var item = (await repo.GetByIdAsync(saved.Id, _tenantId))!;
        var group = AttributeGroup.Create(_tenantId, "NEW-AXIS", "Axis");
        var attribute = AttributeDefinition.Create(_tenantId, group.Id, "NEW-SIZE", "Size", ERP.Domain.Modules.Items.Enums.AttributeDataType.Text, isVariantAxis: true);
        db.Set<AttributeGroup>().Add(group);
        db.Set<AttributeDefinition>().Add(attribute);
        var variant = item.AddVariant([(attribute.Id, "L")], "NEW-VARIANT", 1, _actor);
        await repo.TrackVariantAsync(variant);
        await repo.SaveChangesAsync();
        (await db.ItemVariants.CountAsync()).Should().Be(2);
        db.Entry(variant).Property<Guid>("CompanyId").CurrentValue.Should().Be(_companyA);
    }

    [Fact]
    public async Task Packaging_barcode_conflicts_with_variant_inside_company_but_is_allowed_in_another_company()
    {
        await Save(_companyA, "VARIANT", "CROSS-BAR");
        async Task SavePackaging(Guid company)
        {
            await using var db = Context(company);
            var item = Product(company, "PACKAGING");
            item.ReplacePackagingLevels([("UNIT", 1, 1m, "UNIT", "CROSS-BAR", null, true, true, true)], _actor);
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }
        Func<Task> duplicate = () => SavePackaging(_companyA);
        await duplicate.Should().ThrowAsync<DbUpdateException>();
        await SavePackaging(_companyB);
    }

    [Fact]
    public async Task Missing_company_context_fails_closed()
    {
        await Save(_companyA, "NO-CONTEXT");
        await using var db = Context(Guid.Empty);
        (await new ItemRepository(db).GetAllActiveAsync(_tenantId)).Should().BeEmpty();
        (await db.ItemVariants.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Pricing_cannot_assign_item_from_another_company()
    {
        var a = await Save(_companyA, "PRICING-A");
        await using var db = Context(_companyB);
        var list = PriceList.Create(_tenantId, _companyB, "B", "B List", "USD", false, _actor);
        db.Set<PriceList>().Add(list);
        await db.SaveChangesAsync();
        db.Set<PriceListItem>().Add(PriceListItem.Create(_tenantId, _companyB, list.Id, a.Id, _actor));
        Func<Task> save = () => db.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData(StockMovementType.SaleExit)]
    [InlineData(StockMovementType.PurchaseEntry)]
    public async Task Service_and_foreign_company_items_cannot_enter_inventory(StockMovementType movementType)
    {
        await using var db = Context(_companyA);
        var service = Product(_companyA, "SERVICE", nature: ItemNature.Service);
        var foreign = Product(_companyB, "FOREIGN-PRODUCT");
        db.Items.AddRange(service, foreign);
        await db.SaveChangesAsync();
        var repo = Stock(db, _companyA);
        foreach (var id in new[] { service.Id, foreign.Id })
        {
            Func<Task> append = () => repo.AppendMovementAsync(_tenantId, _companyA, id, _warehouse1, movementType, 1, "UNIT",
                new DateOnly(2026, 10, 5), null, null, null, _actor);
            await append.Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>();
        }
        (await db.StockMovements.ToListAsync()).Should().BeEmpty();
        (await db.CurrentStocks.ToListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Products_always_record_kardex_and_stock_with_line_warehouses_even_when_control_is_off(bool control)
    {
        await using var db = Context(_companyA);
        var product = Product(_companyA, "INVENTORY", control);
        db.Items.Add(product);
        await db.SaveChangesAsync();
        var repo = Stock(db, _companyA);
        foreach (var warehouse in new[] { _warehouse1, _warehouse2 })
            await repo.AppendMovementAsync(_tenantId, _companyA, product.Id, warehouse, StockMovementType.PurchaseEntry, 3, "UNIT",
                new DateOnly(2026, 10, 5), null, null, null, _actor, unitCost: 2);
        await repo.SaveChangesWithSequenceRetryAsync();
        await repo.AppendMovementAsync(_tenantId, _companyA, product.Id, _warehouse1, StockMovementType.SaleExit, -1, "UNIT",
            new DateOnly(2026, 10, 5), null, null, null, _actor);
        await repo.SaveChangesWithSequenceRetryAsync();
        (await db.StockMovements.CountAsync()).Should().Be(3);
        (await repo.GetStockAsync(_tenantId, _warehouse1, product.Id))!.Quantity.Should().Be(2);
        (await repo.GetStockAsync(_tenantId, _warehouse2, product.Id))!.Quantity.Should().Be(3);
    }

    private static StockRepository Stock(ErpDbContext db, Guid company) => new(db, new CurrentCompany(company), new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance);
    private sealed class CurrentTenant(Guid tenant) : ICurrentTenant { public Guid TenantId => tenant; public string? Slug => null; }
    private sealed class CurrentCompany(Guid company) : ICurrentCompany { public Guid CompanyId => company; public bool IsAuthenticated => company != Guid.Empty; public bool HasCompanyContext => company != Guid.Empty; }
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
