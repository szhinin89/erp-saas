using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Modules.Company.Entities;
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
    private Guid _companyId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Item Test Company", createdBy: _createdBy);
        _companyId = company.Id;
        db.Companies.Add(company);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Supplier_07_requires_its_own_active_relation_even_when_another_item_has_sku_07(bool inactiveRelation)
    {
        var (supplierA, supplierB, rice, _) = await SeedSupplier07Async(inactiveRelation, inactiveRelation);

        await using var readDb = CreateContext(_tenantId);
        var repository = new ItemRepository(readDb);
        (await repository.FindItemIdBySupplierCodeAsync(supplierA, " 07 ", _tenantId)).Should().Be(rice);
        (await repository.GetSupplierCodeMatchAsync(supplierA, "07", _tenantId))!.ItemId.Should().Be(rice);
        (await repository.FindItemIdBySupplierCodeAsync(supplierB, "07", _tenantId)).Should().BeNull();
        (await repository.GetSupplierCodeMatchAsync(supplierB, "07", _tenantId)).Should().BeNull();
        var document = Reception(supplierB);
        (await new PurchaseReceptionAutoMatcher(repository).RefreshAsync(document, CancellationToken.None)).Should().Be(0);
        document.Lines.Single().ItemId.Should().BeNull();
        document.Lines.Single().MatchStatus.Should().Be(ItemMatchStatus.Pending);
        (await new ItemMatchFinder(repository).FindCandidatesAsync(_tenantId, supplierB, "07", null, "Azucar", 5))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Supplier_A_07_is_rice_and_supplier_B_07_is_sugar_only_in_the_authenticated_company()
    {
        var (supplierA, supplierB, rice, sugar) = await SeedSupplier07Async(registerB: true);

        await using (var readDb = CreateContext(_tenantId))
        {
            var repository = new ItemRepository(readDb);
            (await repository.FindItemIdBySupplierCodeAsync(supplierA, "07", _tenantId)).Should().Be(rice);
            (await repository.FindItemIdBySupplierCodeAsync(supplierB, "07", _tenantId)).Should().Be(sugar);
            (await repository.GetSupplierCodeMatchAsync(supplierA, "07", _tenantId))!.ItemId.Should().Be(rice);
            (await repository.GetSupplierCodeMatchAsync(supplierB, "07", _tenantId))!.ItemId.Should().Be(sugar);
            foreach (var (supplier, expected) in new[] { (supplierA, rice), (supplierB, sugar) })
            {
                var document = Reception(supplier);
                (await new PurchaseReceptionAutoMatcher(repository).RefreshAsync(document, CancellationToken.None)).Should().Be(1);
                document.Lines.Single().ItemId.Should().Be(expected);
            }
            (await repository.FindItemIdBySupplierCodeAsync(supplierA, "07", Guid.NewGuid())).Should().BeNull();
            (await repository.GetSupplierCodeMatchAsync(supplierA, "07", Guid.NewGuid())).Should().BeNull();
        }

        await using var foreignDb = CreateContext(_tenantId, Guid.NewGuid());
        var foreignRepository = new ItemRepository(foreignDb);
        (await foreignRepository.FindItemIdBySupplierCodeAsync(supplierA, "07", _tenantId)).Should().BeNull();
        (await foreignRepository.GetSupplierCodeMatchAsync(supplierB, "07", _tenantId)).Should().BeNull();
    }

    private async Task<(Guid SupplierA, Guid SupplierB, Guid Rice, Guid Sugar)> SeedSupplier07Async(
        bool registerB = false, bool inactiveB = false)
    {
        await using var db = CreateContext(_tenantId);
        var supplierA = BusinessPartner.Create(_tenantId, "04", "1791352688001", 2, "Proveedor A", _createdBy);
        var supplierB = BusinessPartner.Create(_tenantId, "04", "1791352688002", 2, "Proveedor B", _createdBy);
        var itemType = ItemTypeDefinition.Create(_tenantId, "PHYSICAL", "Fisico", 1, _createdBy);
        Item Product(string sku, string name) => Item.Create(_tenantId, sku, name, name, itemType.Id, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(),
            _createdBy, companyId: _companyId);
        var rice = Product("07", "Arroz");
        var sugar = Product("SUGAR", "Azucar");
        rice.AddSupplierCode("07", false, supplierA.Id, _createdBy);
        if (registerB)
        {
            var relation = sugar.AddSupplierCode("07", false, supplierB.Id, _createdBy);
            if (inactiveB) relation.Disable();
        }
        db.BusinessPartners.AddRange(supplierA, supplierB);
        db.ItemTypes.Add(itemType);
        db.Items.AddRange(rice, sugar);
        await db.SaveChangesAsync();
        return (supplierA.Id, supplierB.Id, rice.Id, sugar.Id);
    }

    private PurchaseReceptionDocument Reception(Guid supplierId)
    {
        var document = PurchaseReceptionDocument.Create(_tenantId, _companyId, Guid.NewGuid(),
            PurchaseReceptionSourceDocType.Invoice, "1791352688001", "Proveedor", supplierId,
            new string('1', 49), "001-001-000000001", new DateOnly(2026, 10, 23), null, 10m, 0m, 10m, _createdBy);
        var line = PurchaseReceptionLine.Create(document.Id, _tenantId, "Producto", 1m, 10m,
            "0", "2", 0m, 0m, 0m, 0m, 10m, 10m, supplierCode: "07");
        document.AttachSriAuthorization("1", DateTime.UtcNow, "<factura/>", DateTime.UtcNow, [line], _createdBy,
            "01", null, new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null));
        return document;
    }

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

            var itemType = ItemTypeDefinition.Create(
                _tenantId,
                "PHYSICAL",
                "Fisico",
                1,
                _createdBy
            );
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
                _createdBy, companyId: _companyId
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

    [Fact]
    public async Task FindCodeOwnerAsync_reports_owner_by_company_and_supplier_without_self_conflict()
    {
        var foreignCompanyId = Guid.NewGuid();
        Guid supplierA, supplierB, ownerId, barcodeId, boxId, foreignId;
        await using (var db = CreateContext(_tenantId))
        {
            var foreignCompany = Company.CreateManaged(_tenantId, "1790012345002", "Foreign Company", createdBy: _createdBy);
            foreignCompanyId = foreignCompany.Id;
            db.Companies.Add(foreignCompany);
            var a = BusinessPartner.Create(_tenantId, "04", "1791352688011", 2, "Proveedor A", _createdBy);
            var b = BusinessPartner.Create(_tenantId, "04", "1791352688012", 2, "Proveedor B", _createdBy);
            var itemType = ItemTypeDefinition.Create(_tenantId, "PHYSICAL", "Fisico", 1, _createdBy);
            var owner = Item.Create(_tenantId, "SKU-OWN", "Arroz Owner", "Arroz Owner", itemType.Id, "UNIT",
                ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(),
                _createdBy, companyId: _companyId);
            var variant = owner.AddVariant(Array.Empty<(Guid, string)>(), null, 0, _createdBy);
            variant.AddBarcode("7861234567898", "EAN13", _tenantId, _createdBy, true);
            owner.ReplacePackagingLevels(
                [
                    ("Unidad", 1, 1m, "UNIT", null, null, true, false, true),
                    ("Caja 12", 2, 12m, "CAJA", "17861234567895", null, false, true, false),
                ], _createdBy);
            owner.AddSupplierCode("SUP-1", false, a.Id, _createdBy);
            owner.AddSupplierCode("OLD-1", false, a.Id, _createdBy).Disable();
            db.BusinessPartners.AddRange(a, b);
            db.ItemTypes.Add(itemType);
            db.Items.Add(owner);
            await db.SaveChangesAsync();
            (supplierA, supplierB, ownerId) = (a.Id, b.Id, owner.Id);
            barcodeId = variant.Barcodes.Single().Id;
            boxId = owner.PackagingLevels.Single(p => !p.IsBaseUnit).Id;

            var foreign = Item.Create(_tenantId, "FOREIGN", "Foreign", "Foreign", itemType.Id, "UNIT",
                ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(),
                _createdBy, companyId: foreignCompanyId);
            foreign.AddVariant(Array.Empty<(Guid, string)>(), null, 0, _createdBy)
                .AddBarcode("FOREIGN-BC", "Internal", _tenantId, _createdBy, true);
            foreignId = foreign.Id;
            await using var foreignDb = CreateContext(_tenantId, foreignCompanyId);
            foreignDb.Items.Add(foreign);
            await foreignDb.SaveChangesAsync();
        }

        await using var readDb = CreateContext(_tenantId);
        var repository = new ItemRepository(readDb);
        Task<ItemCodeOwner?> Find(string kind, string code, Guid? supplier = null, Guid? exclude = null) =>
            repository.FindCodeOwnerAsync(new ItemCodeLookup(kind, code, supplier, exclude), _tenantId);

        (await Find("Sku", " sku-own "))!.Should().Be(new ItemCodeOwner(ownerId, "Arroz Owner"));
        (await Find("Sku", "SKU-OWN", exclude: ownerId)).Should().BeNull("editing keeps its own SKU");
        (await Find("VariantSku", "SKU-OWN"))!.ItemId.Should().Be(ownerId);
        (await Find("Barcode", "7861234567898"))!.ItemId.Should().Be(ownerId);
        (await Find("Barcode", "7861234567898", exclude: barcodeId)).Should().BeNull();
        (await Find("Barcode", "17861234567895"))!.ItemId.Should().Be(ownerId);
        (await Find("Barcode", "17861234567895", exclude: boxId)).Should().BeNull();
        (await Find("SupplierCode", "SUP-1", supplierA))!.ItemId.Should().Be(ownerId);
        (await Find("SupplierCode", "SUP-1", supplierB)).Should().BeNull("supplier codes are scoped per supplier");
        (await Find("SupplierCode", "OLD-1", supplierA)).Should().BeNull("inactive relations do not block");
        (await Find("SupplierCode", "SUP-1")).Should().BeNull("supplier is mandatory");
        (await Find("Sku", "FOREIGN")).Should().BeNull("another company may reuse codes");
        (await Find("Barcode", "FOREIGN-BC")).Should().BeNull();
        (await repository.FindCodeOwnerAsync(new ItemCodeLookup("Sku", "SKU-OWN"), Guid.NewGuid())).Should().BeNull();

        await using var foreignRead = CreateContext(_tenantId, foreignCompanyId);
        var foreignRepository = new ItemRepository(foreignRead);
        (await foreignRepository.FindCodeOwnerAsync(new ItemCodeLookup("Barcode", "FOREIGN-BC"), _tenantId))!
            .ItemId.Should().Be(foreignId);
        (await foreignRepository.FindCodeOwnerAsync(new ItemCodeLookup("Sku", "SKU-OWN"), _tenantId)).Should().BeNull();
    }

    private ErpDbContext CreateContext(Guid tenantId, Guid? companyId = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId ?? _companyId)
        );
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
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
