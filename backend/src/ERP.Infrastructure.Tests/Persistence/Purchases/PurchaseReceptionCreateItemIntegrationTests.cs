using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Purchases.UseCases.PurchaseReception.CreateItemFromReceptionLine;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace ERP.Infrastructure.Tests.Persistence.Purchases;

/// <summary>
/// Suite de integración (PostgreSQL real vía Testcontainers) para "Crear Item desde Purchase
/// Reception Line" — valida la persistencia real de <see cref="Item"/>, <see cref="ItemSupplierCode"/>
/// y la actualización de <see cref="PurchaseReceptionLine"/>. El paso <c>CreateItemCommand</c> (Items)
/// se mockea vía <see cref="IMediator"/> — su propia validación ya está cubierta por la suite de
/// Items; aquí se valida específicamente lo que aporta esta fase: la confirmación de la
/// vinculación (<see cref="ItemMatchConfirmationService"/>) contra una base de datos real.
/// </summary>
public sealed class PurchaseReceptionCreateItemIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_createitem_reception_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _createdBy;
    private Guid _itemTypeId;
    private Guid _supplierId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, _createdBy,
            companyId: company.Id);
        var itemType = ItemTypeDefinition.Create(tenant.Id, "BIEN", "Bien", 1, _createdBy);
        var supplier = BusinessPartner.Create(
            tenant.Id, TaxIdentification.SriRuc, "1791352688001", PersonType.Legal, "QUALA ECUADOR S A", _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.ItemTypes.Add(itemType);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _itemTypeId = itemType.Id;
        _supplierId = supplier.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new NewChildEntityTrackingInterceptor())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(tenantId), new NoOpPublisher(), new FixedCurrentCompany(companyId));
    }

    private ErpDbContext CreateContext() => CreateContext(_tenantId, _companyId);

    private PurchaseReceptionDocument BuildDocumentWithLine(out PurchaseReceptionLine line, string accessKey, string supplierCode)
    {
        var document = PurchaseReceptionDocument.Create(
            _tenantId, _companyId, _branchId, PurchaseReceptionSourceDocType.Invoice,
            "1791352688001", "QUALA ECUADOR S A", _supplierId,
            accessKey, "015-027-000161740", new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            15.96m, 2.4m, 18.35m, _createdBy);
        line = PurchaseReceptionLine.Create(document.Id, _tenantId, "Aceite Girasol 1L", 10m, 2.5m, supplierCode);
        document.AttachSriAuthorization("AUTH-1", DateTime.UtcNow, "<factura/>", DateTime.UtcNow, [line], _createdBy);
        return document;
    }

    [Fact]
    public async Task Handle_persists_the_ItemSupplierCode_and_the_matched_line_against_a_real_database()
    {
        var accessKey = $"AK-{Guid.NewGuid():N}";
        var document = BuildDocumentWithLine(out var line, accessKey, "PROV-100");

        await using (var db = CreateContext())
        {
            var docRepo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));
            await docRepo.AddAsync(document);
            await docRepo.SaveChangesAsync();
        }

        var item = Item.Create(
            _tenantId, "SKU-INTEGRATION-1", "Aceite Girasol 1L", "Aceite Girasol 1L", _itemTypeId, "UNIT",
            ItemTaxConfig.Create(null, null), ItemSaleConfig.Create(), ItemStockConfig.Create(), _createdBy);

        await using (var db = CreateContext())
        {
            var itemRepo = new ItemRepository(db);
            await itemRepo.AddAsync(item);
            await itemRepo.SaveChangesAsync();
        }

        var itemDto = new ItemDto(
            item.Id, item.Code.SKU, item.Code.ShortName, item.Code.Description, _itemTypeId, "Bien",
            null, null, "UNIT", "UNIT", true, false, false, true, false, false, null, true, DateTime.UtcNow, null);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ItemDto>.Success(itemDto));

        await using (var db = CreateContext())
        {
            var docRepo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));
            var itemRepo = new ItemRepository(db);
            var confirmationService = new ItemMatchConfirmationService(itemRepo);
            var handler = new CreateItemFromReceptionLineCommandHandler(
                docRepo, mediator.Object, confirmationService,
                new FixedCurrentTenant(_tenantId), new FixedCurrentUser(_createdBy));

            var command = new CreateItemFromReceptionLineCommand(
                line.Id, "SKU-INTEGRATION-1", "Aceite Girasol 1L", "Aceite Girasol 1L",
                _itemTypeId, Guid.NewGuid(), Guid.NewGuid(), "UNIT", "EAN13");

            var result = await handler.Handle(command, CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
        }

        await using var verifyDb = CreateContext();

        var storedItem = await verifyDb.Items
            .Include(i => i.SupplierCodes)
            .FirstOrDefaultAsync(i => i.Id == item.Id);
        storedItem.Should().NotBeNull();
        storedItem!.SupplierCodes.Should().ContainSingle(c => c.SupplierId == _supplierId && c.Code == "PROV-100");

        var verifyDocRepo = new PurchaseReceptionDocumentRepository(verifyDb, new FixedCurrentCompany(_companyId));
        var storedDocument = await verifyDocRepo.GetByIdAsync(_tenantId, document.Id);
        storedDocument.Should().NotBeNull();
        var storedLine = storedDocument!.Lines.Single(l => l.Id == line.Id);
        storedLine.ItemId.Should().Be(item.Id);
        storedLine.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
        storedLine.MatchedBy.Should().Be(_createdBy);
        storedLine.MatchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_does_not_duplicate_ItemSupplierCode_when_the_supplier_code_already_exists()
    {
        var existingItem = Item.Create(
            _tenantId, "SKU-INTEGRATION-2", "Producto existente", "Producto existente", _itemTypeId, "UNIT",
            ItemTaxConfig.Create(null, null), ItemSaleConfig.Create(), ItemStockConfig.Create(), _createdBy);
        existingItem.AddSupplierCode("PROV-200", isPrimary: true, _supplierId, _createdBy);

        await using (var db = CreateContext())
        {
            var itemRepo = new ItemRepository(db);
            await itemRepo.AddAsync(existingItem);
            await itemRepo.SaveChangesAsync();
        }

        var accessKey = $"AK-{Guid.NewGuid():N}";
        var document = BuildDocumentWithLine(out var line, accessKey, "PROV-200");
        await using (var db = CreateContext())
        {
            var docRepo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));
            await docRepo.AddAsync(document);
            await docRepo.SaveChangesAsync();
        }

        var newItem = Item.Create(
            _tenantId, "SKU-INTEGRATION-3", "Aceite Girasol 1L", "Aceite Girasol 1L", _itemTypeId, "UNIT",
            ItemTaxConfig.Create(null, null), ItemSaleConfig.Create(), ItemStockConfig.Create(), _createdBy);
        await using (var db = CreateContext())
        {
            var itemRepo = new ItemRepository(db);
            await itemRepo.AddAsync(newItem);
            await itemRepo.SaveChangesAsync();
        }

        var itemDto = new ItemDto(
            newItem.Id, newItem.Code.SKU, newItem.Code.ShortName, newItem.Code.Description, _itemTypeId, "Bien",
            null, null, "UNIT", "UNIT", true, false, false, true, false, false, null, true, DateTime.UtcNow, null);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ItemDto>.Success(itemDto));

        await using (var db = CreateContext())
        {
            var docRepo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));
            var itemRepo = new ItemRepository(db);
            var confirmationService = new ItemMatchConfirmationService(itemRepo);
            var handler = new CreateItemFromReceptionLineCommandHandler(
                docRepo, mediator.Object, confirmationService,
                new FixedCurrentTenant(_tenantId), new FixedCurrentUser(_createdBy));

            var command = new CreateItemFromReceptionLineCommand(
                line.Id, "SKU-INTEGRATION-3", "Aceite Girasol 1L", "Aceite Girasol 1L",
                _itemTypeId, Guid.NewGuid(), Guid.NewGuid(), "UNIT", "EAN13");

            var result = await handler.Handle(command, CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
        }

        await using var verifyDb = CreateContext();
        var supplierCodeCount = await verifyDb.Set<ItemSupplierCode>()
            .CountAsync(c => c.TenantId == _tenantId && c.SupplierId == _supplierId && c.Code == "PROV-200");
        supplierCodeCount.Should().Be(1);
    }

    // ── Helpers de identidad para el DbContext ───────────────────────────────

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "test-user";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
