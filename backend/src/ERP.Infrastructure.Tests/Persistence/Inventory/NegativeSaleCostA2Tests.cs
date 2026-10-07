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
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Inventory.Costing;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Accounting.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Modules.Sales.Policies;

namespace ERP.Infrastructure.Tests.Persistence.Inventory;

/// <summary>A2 policy, costing, settlement and concurrency against real PostgreSQL.</summary>
public sealed class NegativeSaleCostA2Tests : IAsyncLifetime
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
    private Guid _branchId;
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
        _branchId = branch.Id;
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
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()).Options,
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

    private sealed class Preferences(bool allow, bool control = true) : ERP.Domain.Configuration.Interfaces.IOperationalPreferencesResolver
    {
        public Task<ERP.Domain.Configuration.Interfaces.OperationalPreferences> ResolveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ERP.Domain.Configuration.Interfaces.OperationalPreferences(
                new(false, false, false, 0m, null, allow, false, null, null), null!, null!,
                new(false, false, false, 0m, control), null!, null!, null!));
        public Task<ERP.Domain.Configuration.Interfaces.OperationalPreferences> ResolveAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default) => ResolveAsync(cancellationToken);
    }

    private Guid _costAccount;
    private async Task<SalesInvoice> AuthorizeProductSale(Item item)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var paymentTerm = PaymentTerm.Create(_tenantId, "CASH", "Cash", 1, 0, _actor);
        var method = PaymentMethod.Create(_tenantId, "CASH", "Cash", false, false, 0, _actor, sriPaymentMethodCode: "01");
        var register = ERP.Domain.Modules.Caja.Entities.CashRegister.Create(_tenantId, _companyA, _branchId, "A2", "A2", _actor);
        var cashAccount = Account.Create(_tenantId, _companyA, AccountCode.Create("1.1.02"), "Cash", null, AccountType.Asset, AccountNature.Debit, true, _actor);
        register.SetAccountingAccount(cashAccount.Id, _actor);
        var establishment = Establishment.Create(_tenantId, _branchId, _companyA, "001", "A2", "Address", null, true, _actor);
        var emission = EmissionPoint.Create(_tenantId, _companyA, establishment.Id, "001", "A2", EmissionType.Electronic, true, _actor);
        var session = ERP.Domain.Modules.Caja.Entities.CashSession.Open(_tenantId, _companyA, _branchId, _actor, register.Id, "A2", "A2", emission.Id, "001", 0m, _actor);
        var invoice = SalesInvoice.CreateDraft(_tenantId, _companyA, _branchId, _supplierId,
            CustomerSnapshot.Create("Customer", "1710034065", "05"), "A2-DRAFT", date, _actor,
            PaymentTermSnapshot.Create(paymentTerm.Id, "Cash", 1, 0), session.Id, emissionType: EmissionType.Physical);
        var line = SalesInvoiceDetail.Create(invoice.Id, _tenantId, "Product", 3m, 10m, "10", itemId: item.Id, warehouseId: _warehouse1, uomCode: "UNIT");
        invoice.ReplaceLines([line], _actor);
        invoice.ReplacePayments([SalesInvoicePayment.Create(invoice.Id, _tenantId, method.Id, "01", "Cash", 34.5m)], _actor);
        await using (var seed = Context(_companyA))
        {
            seed.Accounts.Add(cashAccount); seed.CashRegisters.Add(register); seed.Establishments.Add(establishment);
            seed.EmissionPoints.Add(emission); seed.CashSessions.Add(session); seed.PaymentTerms.Add(paymentTerm);
            seed.PaymentMethods.Add(method); seed.SalesInvoices.Add(invoice); await seed.SaveChangesAsync();
        }
        await using (var db = Context(_companyA))
        {
            var company = new CurrentCompany(_companyA);
            var stock = new StockRepository(db, company, new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance, new Preferences(true));
            var termRepo = new Mock<IPaymentTermRepository>();
            termRepo.Setup(r => r.GetByIdAsync(_tenantId, paymentTerm.Id, It.IsAny<CancellationToken>())).ReturnsAsync(paymentTerm);
            var tax = new Mock<ISriTaxResolver>(); tax.Setup(r => r.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>())).ReturnsAsync(new TaxRateResult(15m, "VAT"));
            var clock = new Mock<ICompanyClock>(); clock.Setup(r => r.TodayAsync(_companyA, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(date);
            var policy = new Mock<ISalesFiscalPolicyResolver>(); policy.Setup(r => r.GetEffectivePolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SalesFiscalPolicyResult(true, 50m, ConsumerFinalMaxAmountSource.Fallback, null));
            var methods = new Mock<IPaymentMethodRepository>(); methods.Setup(r => r.GetByIdAsync(_tenantId, method.Id, It.IsAny<CancellationToken>())).ReturnsAsync(method);
            var maps = new Mock<IPaymentMethodAccountRepository>(); maps.Setup(r => r.GetMapAsync(_tenantId, _companyA, It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<Guid, PaymentMethodAccount>());
            var sessions = new Mock<ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository>(); sessions.Setup(r => r.GetByIdAsync(_tenantId, session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
            var registers = new Mock<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>(); registers.Setup(r => r.GetByIdAsync(_tenantId, register.Id, It.IsAny<CancellationToken>())).ReturnsAsync(register);
            var handler = new AuthorizeSalesInvoiceHandler(new SalesInvoiceRepository(db, company), Mock.Of<ISalesReceivableRepository>(), stock,
                termRepo.Object, tax.Object, Mock.Of<IDocumentSequenceRepository>(), Mock.Of<IEmissionPointRepository>(), Mock.Of<IEstablishmentRepository>(),
                Mock.Of<ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository>(), Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
                clock.Object, Mock.Of<IBusinessPartnerRepository>(), policy.Object, methods.Object, maps.Object,
                Mock.Of<ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository>(), sessions.Object, registers.Object,
                new AccountRepository(db), Mock.Of<IPostingEngine>(), NullLogger<AuthorizeSalesInvoiceHandler>.Instance,
                new CurrentTenant(_tenantId), company, Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId), Mock.Of<ICurrentUser>(u => u.UserId == _actor),
                new Preferences(true), StandardPrecisionPolicyProvider.Instance, new ItemRepository(db), new ERP.Infrastructure.Persistence.Repositories.WarehouseRepository(db, company));
            (await handler.Handle(new(invoice.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        }
        await using var read = Context(_companyA);
        return (await new SalesInvoiceRepository(read, new CurrentCompany(_companyA)).GetByIdAsync(_tenantId, invoice.Id))!;
    }

    [Fact]
    public async Task A2_sale_authorization_with_real_repositories_persists_negative_stock_and_pending_cost()
    {
        var item = await Save(_companyA, "AUTHORIZED"); var invoice = await AuthorizeProductSale(item);
        await using var verify = Context(_companyA);
        invoice.Status.Should().Be(ERP.Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
        (await verify.Set<CurrentStock>().SingleAsync()).Quantity.Should().Be(-3m);
        (await verify.Set<SaleCostObligation>().SingleAsync()).PendingQuantity.Should().Be(3m);
        (await verify.StockMovements.SingleAsync()).SourceDocLineId.Should().Be(invoice.Lines.Single().Id);
    }

    private async Task<StockMovement> ReturnUnits(SalesInvoice invoice, Item item, decimal quantity)
    {
        var original = invoice.Lines.Single();
        var document = SalesReturn.CreateDraft(_tenantId, _companyA, invoice.Id, invoice.CustomerId,
            $"A2-{Guid.NewGuid().ToString("N")[..20]}", "A2 return", _actor);
        var detail = SalesReturnDetail.Create(document.Id, _tenantId, original.Id, original.Description,
            quantity, original.UnitPrice, original.DiscountPct, original.VatCode, original.VatRate, "UNIT",
            item.Id, _warehouse1);
        document.AddLine(detail, _actor);
        document.AddRefundAllocation(SalesReturnRefundAllocation.Create(document.Id, _tenantId,
            ERP.Domain.Modules.Sales.Enums.SalesReturnRefundMethod.Cash, document.GrandTotal), _actor);
        document.Authorize(_actor, 0.01m);
        await using (var seed = Context(_companyA)) { seed.SalesReturns.Add(document); await seed.SaveChangesAsync(); }
        return await Move(item, quantity, type: StockMovementType.SaleReturn, document: document.Id,
            line: detail.Id, sourceType: "SalesReturn");
    }

    [Fact]
    public async Task A2_unknown_return_is_correlated_and_excluded_from_later_COGS()
    {
        var item = await Save(_companyA, "RETURN-UNKNOWN"); var invoice = await AuthorizeProductSale(item);
        var returned = await ReturnUnits(invoice, item, 1m);
        returned.TotalCost.Should().BeNull();
        await SeedCostAccounting(); await Move(item, 5m, 3m); await ProcessCosts(invoice.Id); await ProcessCosts(invoice.Id);
        await using var db = Context(_companyA);
        (await db.JournalEntries.Include(e => e.Lines).ToListAsync()).SelectMany(e => e.Lines)
            .Where(l => l.AccountId == _costAccount).Sum(l => l.Debit - l.Credit).Should().Be(6m);
        (await db.Set<SaleCostAllocation>().Where(a => a.Kind == "Coverage").SumAsync(a => a.PendingQuantity)).Should().Be(2m);
    }

    [Fact]
    public async Task A2_fractional_returns_conserve_recognized_cost_and_inventory_value()
    {
        await SeedCostAccounting(); var item = await Save(_companyA, "FRACTIONAL"); await Move(item, 3m, 0.3333333333m);
        var invoice = await AuthorizeProductSale(item); await ProcessCosts(invoice.Id, 1m);
        var costs = new List<decimal>();
        for (var i = 0; i < 3; i++)
        {
            costs.Add((await ReturnUnits(invoice, item, 1m)).TotalCost!.Value);
            await ProcessCosts(invoice.Id);
        }
        costs.Sum().Should().Be(1m);
        await using var db = Context(_companyA);
        (await db.JournalEntries.Include(e => e.Lines).ToListAsync()).SelectMany(e => e.Lines)
            .Where(l => l.AccountId == _costAccount).Sum(l => l.Debit - l.Credit).Should().Be(0m);
        var stock = await db.Set<CurrentStock>().SingleAsync(); stock.Quantity.Should().Be(3m); stock.TotalStockValue.Should().Be(1m);
        (await db.Set<SaleCostAllocation>().CountAsync()).Should().Be(3);
    }
    private async Task SeedCostAccounting()
    {
        await using var db = Context(_companyA);
        var inventory = Account.Create(_tenantId, _companyA, AccountCode.Create("1.1.01"), "Inventory", null,
            AccountType.Asset, AccountNature.Debit, true, _actor);
        var cogs = Account.Create(_tenantId, _companyA, AccountCode.Create("5.1.01"), "COGS", null,
            AccountType.Expense, AccountNature.Debit, true, _actor);
        _costAccount = cogs.Id;
        db.Accounts.AddRange(inventory, cogs);
        foreach (var reversed in new[] { false, true })
        {
            var rule = PostingRule.Create(_tenantId, _companyA, "Sales",
                reversed ? "CostOfGoodsSoldReversed" : "CostOfGoodsSold", null, null, null, _actor);
            rule.AddLine(cogs.Id, reversed ? AccountNature.Credit : AccountNature.Debit, PostingAmountKind.HistoricalCost);
            rule.AddLine(inventory.Id, reversed ? AccountNature.Debit : AccountNature.Credit, PostingAmountKind.HistoricalCost);
            db.PostingRules.Add(rule);
        }
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        db.AccountingPeriods.Add(AccountingPeriod.Create(_tenantId, _companyA, date.Year, date.Month,
            new(date.Year, date.Month, 1), new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)), _actor));
        await db.SaveChangesAsync();
    }

    private InventoryCostAccounting Accounting(ErpDbContext db, InventoryCostLedger ledger)
    {
        var journals = new JournalEntryRepository(db);
        var periods = new AccountingPeriodRepository(db);
        var sequences = new JournalEntrySequenceRepository(db);
        var engine = new PostingEngine(journals, new PostingRuleRepository(db), periods, sequences,
            new AccountRepository(db), NullLogger<PostingEngine>.Instance);
        var reverse = new ReverseJournalEntryCommandHandler(journals, periods, sequences,
            new CurrentTenant(_tenantId), new CurrentCompany(_companyA), Mock.Of<ICurrentUser>(u => u.UserId == _actor));
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ReverseJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Returns((ReverseJournalEntryCommand c, CancellationToken ct) => reverse.Handle(c, ct));
        return new(ledger, engine, mediator.Object);
    }

    private async Task ProcessCosts(Guid invoice, decimal? original = null)
    {
        await using var db = Context(_companyA);
        var ledger = new InventoryCostLedger(db);
        var service = Accounting(db, ledger);
        if (original.HasValue)
            await service.RecordAsync(_tenantId, _companyA, invoice, invoice, original.Value,
                DateOnly.FromDateTime(DateTime.UtcNow), "Original", _actor, CancellationToken.None);
        else await service.ProcessAsync(_tenantId, _companyA, invoice, CancellationToken.None);
        await ledger.SaveAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A2_real_COGS_provisional_adjustment_retry_and_cancel_have_no_residual()
    {
        await SeedCostAccounting();
        var item = await Save(_companyA, "COGS"); await Move(item, 10m, 2m); await Move(item, -10m);
        var invoice = Guid.NewGuid(); var line = Guid.NewGuid();
        await Move(item, -3m, document: invoice, line: line); await ProcessCosts(invoice, 6m);
        await Move(item, 5m, 3m); await ProcessCosts(invoice); await ProcessCosts(invoice);
        await using (var read = Context(_companyA))
        {
            (await read.JournalEntries.CountAsync()).Should().Be(2);
            (await read.JournalEntries.Include(e => e.Lines).ToListAsync()).SelectMany(e => e.Lines)
                .Where(l => l.AccountId == _costAccount).Sum(l => l.Debit - l.Credit).Should().Be(9m);
        }
        await Move(item, 3m, type: StockMovementType.SaleReturn, document: invoice, line: line);
        await using (var cancel = Context(_companyA))
        {
            var ledger = new InventoryCostLedger(cancel);
            await Accounting(cancel, ledger).CancelAsync(_tenantId, _companyA, invoice, "A2 cancellation", CancellationToken.None);
            await ledger.SaveAsync(CancellationToken.None);
        }
        await using var db = Context(_companyA);
        (await db.JournalEntries.Include(e => e.Lines).ToListAsync()).SelectMany(e => e.Lines)
            .Where(l => l.AccountId == _costAccount).Sum(l => l.Debit - l.Credit).Should().Be(0m);
        (await db.Set<InventoryCostPosting>().Where(p => p.InvoiceId == invoice).ToListAsync()).Should().OnlyContain(p => p.Status == "Canceled");
        (await db.Set<SaleCostObligation>().Where(o => o.InvoiceId == invoice).SingleAsync()).PendingQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task A2_real_COGS_unknown_waits_for_receipt_then_posts_once()
    {
        await SeedCostAccounting(); var item = await Save(_companyA, "PENDING-COGS"); var invoice = Guid.NewGuid();
        await Move(item, -3m, document: invoice); await ProcessCosts(invoice, 0m);
        await using (var read = Context(_companyA)) { (await read.JournalEntries.CountAsync()).Should().Be(0); }
        await Move(item, 5m, 3m); await ProcessCosts(invoice); await ProcessCosts(invoice);
        await using var db = Context(_companyA);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.JournalEntries.Include(e => e.Lines).SingleAsync()).Lines
            .Where(l => l.AccountId == _costAccount).Sum(l => l.Debit).Should().Be(9m);
    }

    [Fact]
    public async Task A2_posting_error_is_durable_and_retry_after_configuration_does_not_duplicate()
    {
        var invoice = Guid.NewGuid(); await ProcessCosts(invoice, 6m);
        await using (var read = Context(_companyA))
        {
            var row = await read.Set<InventoryCostPosting>().SingleAsync();
            row.Status.Should().Be("Failed"); row.ErrorCode.Should().NotBeNullOrEmpty();
            (await read.JournalEntries.CountAsync()).Should().Be(0);
        }
        await SeedCostAccounting(); await ProcessCosts(invoice); await ProcessCosts(invoice);
        await using var db = Context(_companyA);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.Set<InventoryCostPosting>().SingleAsync()).Status.Should().Be("Posted");
    }

    private async Task<StockMovement> Move(Item item, decimal quantity, decimal? cost = null,
        bool allow = true, bool companyControl = true, Guid? warehouse = null,
        StockMovementType? type = null, Guid? document = null, Guid? line = null, string? sourceType = null)
    {
        await using var db = Context(_companyA);
        var repo = new StockRepository(db, new CurrentCompany(_companyA), new PostgresDatabaseExceptionTranslator(),
            StandardPrecisionPolicyProvider.Instance, new Preferences(allow, companyControl));
        var movementType = type ?? (quantity < 0m ? StockMovementType.SaleExit : StockMovementType.PurchaseEntry);
        var result = await repo.AppendMovementAsync(_tenantId, _companyA, item.Id, warehouse ?? _warehouse1,
            movementType, quantity, "UNIT", DateOnly.FromDateTime(DateTime.UtcNow), "A2", document ?? Guid.NewGuid(),
            sourceType ?? (movementType == StockMovementType.PurchaseEntry ? "PurchaseInvoice" : "SalesInvoice"), _actor,
            cost, sourceDocLineId: line ?? Guid.NewGuid());
        // Inventory-only scenarios stage the same durable original cost record that the sales
        // authorization event creates in production; accounting tests then use the real engine.
        if (movementType == StockMovementType.SaleExit && result.TotalCost > 0m)
            db.Set<InventoryCostPosting>().Add(InventoryCostPosting.Create(_tenantId, _companyA,
                result.SourceDocId!.Value, result.SourceDocId.Value, result.TotalCost.Value,
                result.EffectiveDate, "Original", _actor));
        await repo.SaveChangesWithSequenceRetryAsync();
        return result;
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public async Task A2_policy_allows_product_sale_from_zero(bool allow, bool companyControl, bool itemControl)
    {
        var item = Product(_companyA, "ZERO", itemControl);
        await using (var seed = Context(_companyA)) { await new ItemRepository(seed).AddAsync(item); await seed.SaveChangesAsync(); }
        var sale = await Move(item, -3m, allow: allow, companyControl: companyControl);
        sale.ResultQuantity.Should().Be(-3m);
        sale.TotalCost.Should().BeNull();
        sale.CostPending.Should().BeTrue();
        await using var db = Context(_companyA);
        (await db.Set<CurrentStock>().SingleAsync()).Quantity.Should().Be(-3m);
        (await db.Set<SaleCostObligation>().SingleAsync()).ProvisionalUnitCost.Should().BeNull();
    }

    [Fact]
    public async Task A2_stock_two_sale_five_leaves_minus_three_with_provisional_cost()
    {
        var item = await Save(_companyA, "PARTIAL");
        await Move(item, 2m, 2m);
        var sale = await Move(item, -5m);
        sale.PreviousQuantity.Should().Be(2m); sale.ResultQuantity.Should().Be(-3m);
        sale.UnitCost.Should().BeNull(); sale.TotalCost.Should().Be(10m);
        sale.RunningStockValue.Should().Be(-6m); sale.RunningAverageCost.Should().Be(2m);
    }

    [Theory]
    [InlineData(1, -2, -4, 1)]
    [InlineData(3, 0, 0, 3)]
    [InlineData(5, 2, 6, 3)]
    public async Task A2_zero_preserves_cost_and_receipt_regularizes_only_covered_units(int receipt, int quantity, int value, int difference)
    {
        var item = await Save(_companyA, "ZERO-COST");
        await Move(item, 10m, 2m);
        (await Move(item, -10m)).RunningAverageCost.Should().Be(2m);
        var negative = await Move(item, -3m);
        negative.TotalCost.Should().Be(6m);
        var purchase = await Move(item, receipt, 3m);
        purchase.ResultQuantity.Should().Be(quantity); purchase.RunningStockValue.Should().Be(value);
        await using var db = Context(_companyA);
        (await db.Set<SaleCostAllocation>().SumAsync(a => a.CogsAdjustment)).Should().Be(difference);
        (await db.Set<SaleCostObligation>().SumAsync(o => o.PendingQuantity)).Should().Be(Math.Max(0, 3 - receipt));
        (await db.Set<InventoryCostPosting>().Where(p => p.Kind == "Coverage").SumAsync(p => p.Amount)).Should().Be(difference);
    }

    [Fact]
    public async Task A2_no_history_partial_receipts_and_consecutive_sales_preserve_unknown_state()
    {
        var item = await Save(_companyA, "UNKNOWN");
        await Move(item, -3m); await Move(item, -2m);
        await Move(item, 1m, 3m);
        await using (var partial = Context(_companyA))
        {
            var obligations = await partial.Set<SaleCostObligation>().OrderBy(o => o.SequenceNumber).ToListAsync();
            obligations.Select(o => o.PendingQuantity).Should().Equal(2m, 2m);
            (await partial.Set<SaleCostAllocation>().SingleAsync()).ObligationId.Should().Be(obligations[0].Id);
        }
        await Move(item, 6m, 4m);
        await using var db = Context(_companyA);
        var stock = await db.Set<CurrentStock>().SingleAsync();
        stock.Quantity.Should().Be(2m); stock.TotalStockValue.Should().Be(8m);
        (await db.Set<SaleCostAllocation>().SumAsync(a => a.CogsAdjustment)).Should().Be(19m);
        (await db.Set<SaleCostObligation>().SumAsync(o => o.PendingQuantity)).Should().Be(0m);
    }

    [Fact]
    public async Task A2_reservations_block_even_when_physical_quantity_is_sufficient()
    {
        var item = await Save(_companyA, "RESERVED"); await Move(item, 5m, 2m);
        await using (var seed = Context(_companyA)) { (await seed.Set<CurrentStock>().SingleAsync()).Reserve(4m, _actor); await seed.SaveChangesAsync(); }
        Func<Task> sale = () => Move(item, -2m, allow: false);
        await sale.Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>().WithMessage("*available*");
        await using var db = Context(_companyA);
        (await db.Set<CurrentStock>().SingleAsync()).Quantity.Should().Be(5m);
    }

    [Theory]
    [InlineData(StockMovementType.TransferExit)]
    [InlineData(StockMovementType.NegativeAdjust)]
    [InlineData(StockMovementType.PurchaseReturn)]
    public async Task A2_non_sale_outflows_keep_negative_guard(StockMovementType type)
    {
        var item = await Save(_companyA, "GUARDED");
        Func<Task> movement = () => Move(item, -1m, type: type);
        await movement.Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(false, 1, 0)]
    [InlineData(true, 2, -1)]
    public async Task A2_concurrent_sales_serialize_at_item_warehouse(bool allow, int succeeded, int finalQuantity)
    {
        var item = await Save(_companyA, "RACE"); await Move(item, 1m, 2m);
        async Task<bool> Sell() { try { await Move(item, -1m, allow: allow); return true; } catch (ERP.Domain.Exceptions.DomainRuleViolationException) { return false; } }
        var results = await Task.WhenAll(Sell(), Sell());
        results.Count(x => x).Should().Be(succeeded);
        await using var db = Context(_companyA);
        (await db.Set<CurrentStock>().SingleAsync()).Quantity.Should().Be(finalQuantity);
    }

    [Fact]
    public async Task A2_purchase_and_negative_sale_concurrent_preserve_stock_and_value()
    {
        var item = await Save(_companyA, "RECEIPT-RACE");
        await Move(item, 10m, 2m); await Move(item, -10m);
        await Task.WhenAll(Move(item, -3m), Move(item, 5m, 3m));
        await using var db = Context(_companyA);
        var stock = await db.Set<CurrentStock>().SingleAsync();
        stock.Quantity.Should().Be(2m); stock.TotalStockValue.Should().Be(6m);
    }

    [Fact]
    public async Task A2_other_warehouse_and_company_do_not_cover_the_deficit()
    {
        var item = await Save(_companyA, "SCOPE"); await Move(item, -3m);
        await Move(item, 5m, 3m, warehouse: _warehouse2);
        await using var db = Context(_companyA);
        (await db.Set<SaleCostObligation>().SingleAsync()).PendingQuantity.Should().Be(3m);
        await using var foreign = Context(_companyB);
        (await foreign.Set<SaleCostObligation>().CountAsync()).Should().Be(0);
        (await foreign.Set<SaleCostAllocation>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A2_partial_return_extinguishes_pending_first_then_prorates_recognized_cost()
    {
        var item = await Save(_companyA, "RETURN"); var invoice = Guid.NewGuid(); var line = Guid.NewGuid();
        await Move(item, -3m, document: invoice, line: line);
        await Move(item, 1m, 3m);
        var returned = await Move(item, 1m, type: StockMovementType.SaleReturn, document: invoice, line: line);
        returned.TotalCost.Should().BeNull();
        await Move(item, 1m, 5m);
        await using var db = Context(_companyA);
        var obligation = await db.Set<SaleCostObligation>().SingleAsync();
        obligation.PendingQuantity.Should().Be(0m); obligation.ResolvedQuantity.Should().Be(2m);
        obligation.ResolvedCost.Should().Be(8m);
        obligation.ReturnedQuantity.Should().Be(1m);
        (await db.Set<SaleCostAllocation>().CountAsync()).Should().Be(3);
    }

    private sealed class CurrentTenant(Guid tenant) : ICurrentTenant { public Guid TenantId => tenant; public string? Slug => null; }
    private sealed class CurrentCompany(Guid company) : ICurrentCompany { public Guid CompanyId => company; public bool IsAuthenticated => company != Guid.Empty; public bool HasCompanyContext => company != Guid.Empty; }
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
