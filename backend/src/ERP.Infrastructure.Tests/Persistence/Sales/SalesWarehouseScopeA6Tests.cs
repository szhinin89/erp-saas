using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Sales;

[Trait("Category", "PostgreSql")]
public sealed class SalesWarehouseScopeA6Tests(SalesWarehouseA6Fixture f) : IClassFixture<SalesWarehouseA6Fixture>
{
    [Theory]
    [InlineData("missing")]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("branch")]
    [InlineData("empty")]
    public async Task Invalid_warehouse_is_rejected_by_all_four_flows_without_changes(string kind)
    {
        var warehouseId = f.Invalid[kind];
        await using var db = f.Context();
        var existing = await f.SeedDraftAsync(db, kind == "missing" || kind == "empty" ? f.WarehouseA : warehouseId);
        var before = await f.SnapshotAsync(existing.Id);
        var lines = f.Lines(warehouseId);
        var create = await f.Create(db).Handle(new(f.CustomerId, f.Today, lines), CancellationToken.None);
        create.IsSuccess.Should().BeFalse();
        create.Error.Should().Contain("bodega");
        var update = await f.Update(db).Handle(new(existing.Id, f.CustomerId, f.Today, lines, Notes: "MUST NOT SAVE"), CancellationToken.None);
        update.IsSuccess.Should().BeFalse();
        update.Error.Should().Contain("bodega");
        var search = await f.Search(db).Handle(new("A6", warehouseId), CancellationToken.None);
        search.IsSuccess.Should().BeFalse();
        search.Error.Should().Contain("bodega");
        // The FK prevents persisting missing/empty ids. Exercise authorization with the
        // same loaded aggregate carrying a stale invalid id, without bypassing constraints.
        if (kind == "missing" || kind == "empty")
            db.Entry(existing.Lines.Single()).Property(l => l.WarehouseId).CurrentValue = warehouseId;
        var sequences = new Mock<IDocumentSequenceRepository>(MockBehavior.Strict);
        var emission = new Mock<ISalesInvoiceEmissionStrategyResolver>(MockBehavior.Strict);
        var authorize = await f.Authorize(db, sequences.Object, emission.Object).Handle(new(existing.Id), CancellationToken.None);
        authorize.IsSuccess.Should().BeFalse();
        authorize.Error.Should().Contain("bodega");
        existing.Status.Should().Be(SalesInvoiceStatus.Draft);
        existing.DomainEvents.Should().BeEmpty();
        sequences.Invocations.Should().BeEmpty();
        emission.Invocations.Should().BeEmpty();
        (await f.SnapshotAsync(existing.Id)).Should().Be(before);
    }

    [Fact]
    public async Task Two_valid_warehouses_in_same_branch_survive_create_update_and_authorize()
    {
        await using var db = f.Context();
        var lines = f.Lines(f.WarehouseA);
        lines.AddRange(f.Lines(f.WarehouseB));
        var create = await f.Create(db).Handle(new(f.CustomerId, f.Today, lines,
            Payments: [new(f.Method.Id, 23m)]), CancellationToken.None);
        create.IsSuccess.Should().BeTrue(create.Error);
        var id = create.Value!.Id;
        var update = await f.Update(db).Handle(new(id, f.CustomerId, f.Today, lines,
            Payments: [new(f.Method.Id, 23m)]), CancellationToken.None);
        update.IsSuccess.Should().BeTrue(update.Error);
        var sequences = new Mock<IDocumentSequenceRepository>();
        sequences.Setup(r => r.CaptureNextAsync(f.TenantId, f.CompanyId, f.EmissionId, "01", It.IsAny<CancellationToken>()))
            .ReturnsAsync("000000001");
        var strategy = new Mock<ISalesInvoiceEmissionStrategy>();
        strategy.Setup(r => r.ExecuteAsync(It.IsAny<SalesInvoiceEmissionContext>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var emission = new Mock<ISalesInvoiceEmissionStrategyResolver>();
        emission.Setup(r => r.Resolve(EmissionType.Physical)).Returns(strategy.Object);
        var authorize = await f.Authorize(db, sequences.Object, emission.Object).Handle(new(id), CancellationToken.None);
        authorize.IsSuccess.Should().BeTrue(authorize.Error);
        await using var read = f.Context();
        var invoice = (await new SalesInvoiceRepository(read, f.Company).GetByIdAsync(f.TenantId, id))!;
        invoice.Status.Should().Be(SalesInvoiceStatus.Authorized);
        invoice.Lines.Select(l => l.WarehouseId).Should().BeEquivalentTo(new Guid?[] { f.WarehouseA, f.WarehouseB });
        var movements = await read.StockMovements.Where(m => m.SourceDocId == id).ToListAsync();
        movements.Should().HaveCount(2);
        movements.Select(m => m.WarehouseId).Should().BeEquivalentTo(new[] { f.WarehouseA, f.WarehouseB });
        movements.Should().OnlyContain(m => m.BranchId == f.BranchId && m.CompanyId == f.CompanyId && m.Quantity == -1m);
        var stock = new StockRepository(read, f.Company, new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance);
        foreach (var warehouse in new[] { f.WarehouseA, f.WarehouseB })
            (await stock.GetStockAsync(f.TenantId, warehouse, f.ItemId))!.Quantity.Should().Be(9m);
        // q shorter than two intentionally exercises the warehouse guard independently of search enrichment.
        foreach (var warehouse in new[] { f.WarehouseA, f.WarehouseB })
            (await f.Search(read).Handle(new("A", warehouse), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }
}

public sealed class SalesWarehouseA6Fixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly Guid _actor = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid WarehouseA { get; private set; }
    public Guid WarehouseB { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid EmissionId { get; private set; }
    private Guid _sessionId;
    private Guid _registerId;
    public PaymentTerm Term { get; private set; } = null!;
    public PaymentMethod Method { get; private set; } = null!;
    public Dictionary<string, Guid> Invalid { get; } = new();
    public DateOnly Today => new(2026, 10, 6);
    public ICurrentCompany Company => new CurrentCompany(CompanyId);
    private ICurrentTenant Tenant => new CurrentTenant(TenantId);
    private ICurrentBranch Branch => Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId);
    private ICurrentUser User => Mock.Of<ICurrentUser>(u => u.UserId == _actor);

    public ErpDbContext Context() => new(
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()).Options,
        Tenant, new NoOpPublisher(), Company);

    private Branch NewBranch(Guid tenant, Guid company, string code) =>
        ERP.Domain.Branches.Entities.Branch.Create(tenant, code, "Address", code, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, code == "001", _actor, companyId: company);
    private Warehouse NewWarehouse(Guid tenant, Guid company, Guid branch, string code) =>
        Warehouse.Create(tenant, branch, code, code, null, null, null, null, null, null, null, null, null, _actor, company);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Context();
        await db.Database.MigrateAsync();
        var tenant = ERP.Domain.Tenants.Entities.Tenant.Create("A6", $"a6-{Guid.NewGuid():N}"[..16], _actor);
        var foreignTenant = ERP.Domain.Tenants.Entities.Tenant.Create("A6 foreign", $"a6-{Guid.NewGuid():N}"[..16], _actor);
        var company = ERP.Domain.Modules.Company.Entities.Company.CreateManaged(tenant.Id, "1790012345001", "A6", createdBy: _actor);
        var foreignCompany = ERP.Domain.Modules.Company.Entities.Company.CreateManaged(tenant.Id, "1790012345002", "A6 company", createdBy: _actor);
        var foreignTenantCompany = ERP.Domain.Modules.Company.Entities.Company.CreateManaged(foreignTenant.Id, "1790012345003", "A6 tenant", createdBy: _actor);
        var branch = NewBranch(tenant.Id, company.Id, "001");
        var otherBranch = NewBranch(tenant.Id, company.Id, "002");
        var otherCompanyBranch = NewBranch(tenant.Id, foreignCompany.Id, "003");
        var otherTenantBranch = NewBranch(foreignTenant.Id, foreignTenantCompany.Id, "004");
        var a = NewWarehouse(tenant.Id, company.Id, branch.Id, "A");
        var b = NewWarehouse(tenant.Id, company.Id, branch.Id, "B");
        var crossBranch = NewWarehouse(tenant.Id, company.Id, otherBranch.Id, "C");
        var crossCompany = NewWarehouse(tenant.Id, foreignCompany.Id, otherCompanyBranch.Id, "D");
        var crossTenant = NewWarehouse(foreignTenant.Id, foreignTenantCompany.Id, otherTenantBranch.Id, "E");
        TenantId = tenant.Id; CompanyId = company.Id; BranchId = branch.Id;
        WarehouseA = a.Id; WarehouseB = b.Id;
        Invalid["missing"] = Guid.NewGuid(); Invalid["empty"] = Guid.Empty;
        Invalid["branch"] = crossBranch.Id; Invalid["company"] = crossCompany.Id; Invalid["tenant"] = crossTenant.Id;
        var type = ItemTypeDefinition.Create(TenantId, "A6", "A6", 0, _actor);
        var item = Item.Create(TenantId, "A6-PRODUCT", "A6 product", "A6 product", type.Id, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(), _actor, companyId: CompanyId);
        ItemId = item.Id;
        var customer = BusinessPartner.Create(TenantId, "05", "1710034065", 1, "A6 Customer", _actor);
        CustomerId = customer.Id;
        Term = PaymentTerm.Create(TenantId, "CASH", "Cash", 1, 0, _actor);
        Method = PaymentMethod.Create(TenantId, "CASH", "Cash", false, false, 0, _actor, sriPaymentMethodCode: "01");
        var account = Account.Create(TenantId, CompanyId, AccountCode.Create("1.1.02"), "Cash", null, AccountType.Asset, AccountNature.Debit, true, _actor);
        var register = CashRegister.Create(TenantId, CompanyId, BranchId, "A6", "A6", _actor);
        register.SetAccountingAccount(account.Id, _actor); _registerId = register.Id;
        var establishment = Establishment.Create(TenantId, BranchId, CompanyId, "001", "A6", "Address", null, true, _actor);
        var emission = EmissionPoint.Create(TenantId, CompanyId, establishment.Id, "001", "A6", EmissionType.Physical, true, _actor);
        EmissionId = emission.Id;
        var session = CashSession.Open(TenantId, CompanyId, BranchId, _actor, register.Id, "A6", "A6", emission.Id, "001", 0m, _actor);
        _sessionId = session.Id;
        db.Tenants.AddRange(tenant, foreignTenant);
        db.Companies.AddRange(company, foreignCompany, foreignTenantCompany);
        db.Branches.AddRange(branch, otherBranch, otherCompanyBranch, otherTenantBranch);
        db.Warehouses.AddRange(a, b, crossBranch, crossCompany, crossTenant);
        db.ItemTypes.Add(type); db.Items.Add(item); db.BusinessPartners.Add(customer);
        db.Set<BusinessPartnerRole>().Add(BusinessPartnerRole.Create(TenantId, customer.Id, RoleType.Customer, _actor));
        db.PaymentTerms.Add(Term); db.PaymentMethods.Add(Method); db.Accounts.Add(account);
        db.CashRegisters.Add(register); db.Establishments.Add(establishment); db.EmissionPoints.Add(emission); db.CashSessions.Add(session);
        await db.SaveChangesAsync();
        await using var stockDb = Context();
        var stock = new StockRepository(stockDb, Company, new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance);
        foreach (var warehouse in new[] { WarehouseA, WarehouseB })
            await stock.AppendMovementAsync(TenantId, CompanyId, ItemId, warehouse, StockMovementType.PurchaseEntry, 10m, "UNIT", Today, null, null, null, _actor, unitCost: 1m);
        await stock.SaveChangesWithSequenceRetryAsync();
    }
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public List<SalesLineInput> Lines(Guid warehouse) => [new(ItemId, "A6 Product", 1m, 10m, "10", WarehouseId: warehouse)];
    public async Task<SalesInvoice> SeedDraftAsync(ErpDbContext db, Guid warehouse)
    {
        var invoice = SalesInvoice.CreateDraft(TenantId, CompanyId, BranchId, CustomerId,
            CustomerSnapshot.Create("A6 Customer", "1710034065", "05"), $"A6-{Guid.NewGuid():N}"[..14], Today, _actor,
            PaymentTermSnapshot.Create(Term.Id, Term.Name, 1, 0), _sessionId, emissionType: EmissionType.Physical);
        var line = SalesInvoiceDetail.Create(invoice.Id, TenantId, "Original", 1m, 10m, "10", itemId: ItemId, warehouseId: warehouse, uomCode: "UNIT");
        line.ApplyTaxes("10", 15m, "VAT", null, 0m, null);
        invoice.ReplaceLines([line], _actor);
        invoice.ReplacePayments([SalesInvoicePayment.Create(invoice.Id, TenantId, Method.Id, "01", "Cash", 11.5m)], _actor);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    public async Task<string> SnapshotAsync(Guid id)
    {
        await using var db = Context();
        var invoice = (await new SalesInvoiceRepository(db, Company).GetByIdAsync(TenantId, id))!;
        return JsonSerializer.Serialize(new {
            Invoice = invoice,
            Invoices = await db.SalesInvoices.CountAsync(),
            Movements = await db.StockMovements.CountAsync(),
            Stock = await db.CurrentStocks.OrderBy(s => s.Id).Select(s => new { s.Id, s.Quantity, s.ReservedQuantity, s.TotalStockValue }).ToListAsync(),
            Receivables = await db.SalesReceivables.CountAsync(),
            Journals = await db.JournalEntries.CountAsync(),
            Sequences = await db.DocumentSequences.CountAsync(),
            Electronic = await db.ElectronicDocuments.CountAsync(),
            Cash = await db.CashMovements.CountAsync()
        });
    }

    private Mock<IPaymentTermDefaultResolver> Terms()
    {
        var repo = new Mock<IPaymentTermDefaultResolver>();
        repo.Setup(r => r.ResolveForSaleAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<PaymentTerm>.Success(Term));
        return repo;
    }
    private Mock<IBusinessPartnerContactRepository> Contacts()
    {
        var repo = new Mock<IBusinessPartnerContactRepository>();
        repo.Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<BusinessPartnerContact>());
        return repo;
    }
    private Mock<IBusinessPartnerLocationRepository> Locations()
    {
        var repo = new Mock<IBusinessPartnerLocationRepository>();
        repo.Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<BusinessPartnerLocation>());
        return repo;
    }
    private Mock<IPricingResolver> Pricing()
    {
        var repo = new Mock<IPricingResolver>();
        repo.Setup(r => r.ResolveManyAsync(It.IsAny<PricingBatchContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyDictionary<Guid, PricingResult>>.Success(new Dictionary<Guid, PricingResult>()));
        return repo;
    }
    private Mock<ISriTaxResolver> Tax()
    {
        var repo = new Mock<ISriTaxResolver>();
        repo.Setup(r => r.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>())).ReturnsAsync(new TaxRateResult(15m, "VAT"));
        return repo;
    }
    private Mock<IPriceListSelectionResolver> Lists()
    {
        var repo = new Mock<IPriceListSelectionResolver>();
        repo.Setup(r => r.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PriceListSelectionResult>());
        return repo;
    }
    private Mock<ICompanySpecialTaxResponsibilityRepository> Responsibilities()
    {
        var repo = new Mock<ICompanySpecialTaxResponsibilityRepository>();
        repo.Setup(r => r.GetResponsibleSriTaxCategoryCodesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
        return repo;
    }
    private IOperationalPreferencesResolver Preferences() => Mock.Of<IOperationalPreferencesResolver>(p =>
        p.ResolveAsync(It.IsAny<CancellationToken>()) == Task.FromResult(new OperationalPreferences(
            new(true, false, true, 0m, null, false, false, null, null), null!, null!, new(false, false, false, 0m, true), null!, null!, null!)));

    public CreateSalesDraftHandler Create(ErpDbContext db) => new(
        new SalesInvoiceRepository(db, Company), new BusinessPartnerRepository(db), new BusinessPartnerRoleRepository(db),
        Contacts().Object, Locations().Object, Terms().Object, new PaymentMethodRepository(db), new ItemRepository(db),
        new EmissionPointRepository(db), Tax().Object, Pricing().Object, Lists().Object, Responsibilities().Object,
        new WarehouseRepository(db, Company), Mock.Of<IAverageCostService>(), Tenant, Company, Branch, User,
        Mock.Of<ICurrentCashSession>(s => s.HasOpenSession == true && s.CashSessionId == _sessionId && s.EmissionPointId == EmissionId),
        Preferences(), Mock.Of<ISalesCreditRequirementPolicy>(), Mock.Of<ICompanyBankAccountRepository>(), StandardPrecisionPolicyProvider.Instance);

    public UpdateSalesDraftHandler Update(ErpDbContext db) => new(
        new UnitOfWork(db), new SalesInvoiceRepository(db, Company), new BusinessPartnerRepository(db), new BusinessPartnerRoleRepository(db),
        Contacts().Object, Locations().Object, Terms().Object, new PaymentMethodRepository(db), new ItemRepository(db), Tax().Object,
        Pricing().Object, Lists().Object, Responsibilities().Object, new WarehouseRepository(db, Company), Mock.Of<IAverageCostService>(),
        Tenant, Company, Branch, User, Preferences(), Mock.Of<ISalesCreditRequirementPolicy>(), Mock.Of<ICompanyBankAccountRepository>(),
        StandardPrecisionPolicyProvider.Instance);

    public SearchItemsForInvoiceHandler Search(ErpDbContext db) => new(
        new InvoiceItemSearchRepository(db), Mock.Of<ISriCatalogResolver>(), Pricing().Object, Tenant, Company, Preferences(), Branch,
        new WarehouseRepository(db, Company));

    public AuthorizeSalesInvoiceHandler Authorize(ErpDbContext db, IDocumentSequenceRepository sequences, ISalesInvoiceEmissionStrategyResolver emission)
    {
        var terms = new Mock<IPaymentTermRepository>();
        terms.Setup(r => r.GetByIdAsync(TenantId, Term.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Term);
        var clock = new Mock<ICompanyClock>();
        clock.Setup(r => r.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(Today);
        var maps = new Mock<IPaymentMethodAccountRepository>();
        maps.Setup(r => r.GetMapAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<Guid, PaymentMethodAccount>());
        return new(new SalesInvoiceRepository(db, Company), new SalesReceivableRepository(db, Company),
            new StockRepository(db, Company, new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance, Preferences()),
            terms.Object, Tax().Object, sequences, new EmissionPointRepository(db), new EstablishmentRepository(db),
            Mock.Of<IElectronicDocumentRepository>(), emission, clock.Object, new BusinessPartnerRepository(db), Mock.Of<ISalesFiscalPolicyResolver>(),
            new PaymentMethodRepository(db), maps.Object, Mock.Of<ICompanyBankAccountRepository>(), new ERP.Infrastructure.Persistence.Repositories.Caja.CashSessionRepository(db, Company),
            new ERP.Infrastructure.Persistence.Repositories.Caja.CashRegisterRepository(db, Company), new AccountRepository(db), Mock.Of<IPostingEngine>(),
            NullLogger<AuthorizeSalesInvoiceHandler>.Instance, Tenant, Company, Branch, User, Preferences(), StandardPrecisionPolicyProvider.Instance,
            new ItemRepository(db), new WarehouseRepository(db, Company));
    }
    private sealed class CurrentTenant(Guid id) : ICurrentTenant { public Guid TenantId => id; public string? Slug => null; }
    private sealed class CurrentCompany(Guid id) : ICurrentCompany { public Guid CompanyId => id; public bool IsAuthenticated => id != Guid.Empty; public bool HasCompanyContext => id != Guid.Empty; }
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
