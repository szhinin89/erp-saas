using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.SriCatalogs;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Seeding;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Data.Common;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Seeding;

[Trait("Category", "PostgreSql")]
public sealed class IgnoreQueryFiltersScopeIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_ignore_query_filters_scope_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _actorId = Guid.NewGuid();
    private IReadOnlyList<CompanyScope> _scopes = [];

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenantA = Tenant.Create("Scope Tenant A", $"scope-a-{Guid.NewGuid():N}"[..16], _actorId);
        var tenantB = Tenant.Create("Scope Tenant B", $"scope-b-{Guid.NewGuid():N}"[..16], _actorId);
        var companyA = Company.CreateManaged(tenantA.Id, "1790012345001", "Company A", createdBy: _actorId);
        var companyB = Company.CreateManaged(tenantA.Id, "1790012345002", "Company B", createdBy: _actorId);
        var companyC = Company.CreateManaged(tenantB.Id, "1790012345003", "Company C", createdBy: _actorId);

        db.Tenants.AddRange(tenantA, tenantB);
        db.Companies.AddRange(companyA, companyB, companyC);
        await db.SaveChangesAsync();

        var supplierA = BusinessPartner.Create(tenantA.Id, "04", "1791352688001", 2, "Supplier A", _actorId);
        var supplierB = BusinessPartner.Create(tenantB.Id, "04", "1791352688001", 2, "Supplier B", _actorId);
        var paymentTermA = PaymentTerm.Create(tenantA.Id, "CONT", "Contado", 1, 0, _actorId);
        var paymentTermB = PaymentTerm.Create(tenantB.Id, "CONT", "Contado", 1, 0, _actorId);
        var itemTypeA = ItemTypeDefinition.Create(tenantA.Id, "MERCH", "Mercadería", 1, _actorId);
        var itemTypeB = ItemTypeDefinition.Create(tenantB.Id, "MERCH", "Mercadería", 1, _actorId);
        db.BusinessPartners.AddRange(supplierA, supplierB);
        db.PaymentTerms.AddRange(paymentTermA, paymentTermB);
        db.ItemTypes.AddRange(itemTypeA, itemTypeB);
        await db.SaveChangesAsync();

        var itemA = CreateItem(tenantA.Id, itemTypeA.Id, "ITEM-A");
        var itemB = CreateItem(tenantB.Id, itemTypeB.Id, "ITEM-C");
        db.Items.AddRange(itemA, itemB);
        await db.SaveChangesAsync();

        var scopeA = await CreateCompanyScopeAsync(db, tenantA, companyA, supplierA, paymentTermA, itemA, 1);
        var scopeB = await CreateCompanyScopeAsync(db, tenantA, companyB, supplierA, paymentTermA, itemA, 2);
        var scopeC = await CreateCompanyScopeAsync(db, tenantB, companyC, supplierB, paymentTermB, itemB, 3);
        _scopes = [scopeA, scopeB, scopeC];
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Backfills_and_remediation_stay_inside_tenant_and_company_scope()
    {
        await using var db = CreateContext();

        var bankBackfill = new BankCatalogBackfillService(
            db,
            new BankCatalogSeeder(db, NullLogger<BankCatalogSeeder>.Instance),
            NullLogger<BankCatalogBackfillService>.Instance
        );
        var bankResult = await bankBackfill.RunAsync();
        bankResult.TenantsProcessed.Should().Be(2);
        bankResult.RowsInserted.Should().Be(18);

        var bankCounts = await db.Banks
            .IgnoreQueryFilters()
            .GroupBy(bank => bank.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.TenantId, row => row.Count);
        bankCounts.Should().ContainKey(_scopes[0].TenantId).WhoseValue.Should().Be(9);
        bankCounts.Should().ContainKey(_scopes[2].TenantId).WhoseValue.Should().Be(9);

        var raceInterceptor = new AssignAccountAfterInitialCashScanInterceptor(
            _postgres.GetConnectionString(),
            _scopes[0].ConcurrentCashRegisterId!.Value,
            _scopes[0].AlternateAccountId
        );
        await using var cashDb = CreateContext(raceInterceptor);
        var cashBackfill = new CashRegisterAccountingAccountBackfillService(
            cashDb,
            NullLogger<CashRegisterAccountingAccountBackfillService>.Instance
        );
        var cashResult = await cashBackfill.RunAsync();
        cashResult.CompaniesProcessed.Should().Be(3);
        cashResult.RowsUpdated.Should().Be(3);

        var cashRegisters = await cashDb.CashRegisters
            .IgnoreQueryFilters()
            .ToDictionaryAsync(register => register.Id);
        foreach (var scope in _scopes)
            cashRegisters[scope.CashRegisterId].AccountingAccountId.Should().Be(scope.CajaGeneralAccountId);
        cashRegisters[_scopes[0].ConfiguredCashRegisterId].AccountingAccountId
            .Should()
            .Be(_scopes[0].AlternateAccountId, "a manual account must never be overwritten");
        cashRegisters[_scopes[0].ConcurrentCashRegisterId!.Value].AccountingAccountId
            .Should()
            .Be(_scopes[0].AlternateAccountId, "the second read must preserve an account assigned after the initial scan");

        var shadowInCompanyB = JournalEntry.Create(
            _scopes[1].TenantId,
            _scopes[1].CompanyId,
            _scopes[1].PeriodDate,
            _scopes[1].AccountingPeriodId,
            _scopes[1].PeriodDate.Year,
            "Purchases",
            "PurchaseReturn",
            _scopes[0].PurchaseReturnId,
            "Cross-company duplicate guard fixture",
            _actorId
        );
        db.JournalEntries.Add(shadowInCompanyB);
        await db.SaveChangesAsync();

        var postingEngine = new PersistingPostingEngine(
            db,
            _scopes,
            _actorId,
            new CurrentTenantService(new HttpContextAccessor()),
            new CurrentCompanyService(new HttpContextAccessor())
        );
        var remediation = new PurchaseReturnPostingRemediationService(
            db,
            postingEngine,
            new AlwaysTodayCompanyClock(),
            NullLogger<PurchaseReturnPostingRemediationService>.Instance
        );

        var dryRun = await remediation.RunAsync(apply: false);
        dryRun.MissingCount.Should().Be(3);
        postingEngine.Facts.Should().BeEmpty("dry-run must not post or write");

        var apply = await remediation.RunAsync(apply: true);
        apply.PostedNowCount.Should().Be(3);
        apply.FailedCount.Should().Be(0);
        postingEngine.Facts.Should().HaveCount(3);

        foreach (var scope in _scopes)
        {
            var fact = postingEngine.Facts.Single(item => item.SourceEventId == scope.PurchaseReturnId);
            fact.TenantId.Should().Be(scope.TenantId);
            fact.CompanyId.Should().Be(scope.CompanyId);
        }

        var returnAEntries = await db.JournalEntries
            .IgnoreQueryFilters()
            .Where(entry => entry.SourceEventId == _scopes[0].PurchaseReturnId)
            .ToListAsync();
        returnAEntries.Should().Contain(entry =>
            entry.TenantId == _scopes[0].TenantId && entry.CompanyId == _scopes[0].CompanyId);
        returnAEntries.Should().Contain(entry =>
            entry.TenantId == _scopes[1].TenantId && entry.CompanyId == _scopes[1].CompanyId);

        var secondApply = await remediation.RunAsync(apply: true);
        secondApply.AlreadyPostedCount.Should().Be(3);
        secondApply.PostedNowCount.Should().Be(0);
        postingEngine.Facts.Should().HaveCount(3, "deduplication is scoped and idempotent");
    }

    [Fact]
    public async Task PaymentMethod_sri_backfill_writes_each_tenant_inside_its_own_scope_and_is_idempotent()
    {
        var tenantA = _scopes[0].TenantId;
        var tenantB = _scopes[2].TenantId;
        await using (var seedDb = CreateContext())
        {
            seedDb.PaymentMethods.AddRange(
                PaymentMethod.Create(tenantA, "EFECTIVO", "Efectivo", false, false, 1, _actorId),
                PaymentMethod.Create(tenantA, "TARJETA", "Tarjeta", true, false, 2, _actorId),
                PaymentMethod.Create(tenantA, "CREDITO", "Crédito", false, true, 5, _actorId),
                PaymentMethod.Create(tenantA, "CHEQUE", "Cheque", true, false, 4, _actorId, sriPaymentMethodCode: "01"),
                PaymentMethod.Create(tenantB, "EFECTIVO", "Efectivo", false, false, 1, _actorId),
                PaymentMethod.Create(tenantB, "TRANSFERENCIA", "Transferencia", true, false, 3, _actorId)
            );
            await seedDb.SaveChangesAsync();
        }

        var saveScope = new PaymentMethodSaveScopeInterceptor();
        await using var db = CreateContext(saveScope);
        var backfill = new PaymentMethodSriMappingBackfillService(
            db,
            new SriCatalogLookupRepository(db),
            NullLogger<PaymentMethodSriMappingBackfillService>.Instance
        );

        var first = await backfill.RunAsync();
        first.CandidatesFound.Should().Be(5);
        first.RowsUpdated.Should().Be(4);
        first.SkippedNoMapping.Should().Be(1, "CREDITO no tiene código SRI sugerido");
        first.SkippedInactiveCatalog.Should().Be(0);
        JobTenantContext.Current.Should().Be(Guid.Empty, "el contexto de job se restaura al terminar");

        saveScope.SavedTenants.Should().BeEquivalentTo(
            new[] { tenantA, tenantB },
            "un SaveChanges por tenant, cada uno dentro de su propio JobExecutionContext"
        );

        var codes = await db.PaymentMethods
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(pm => pm.TenantId == tenantA || pm.TenantId == tenantB)
            .ToDictionaryAsync(pm => (pm.TenantId, pm.Code), pm => pm.SriPaymentMethodCode);
        codes[(tenantA, "EFECTIVO")].Should().Be("01");
        codes[(tenantA, "TARJETA")].Should().Be("19");
        codes[(tenantA, "CREDITO")].Should().BeNull();
        codes[(tenantA, "CHEQUE")].Should().Be("01", "un mapeo manual nunca se sobrescribe");
        codes[(tenantB, "EFECTIVO")].Should().Be("01");
        codes[(tenantB, "TRANSFERENCIA")].Should().Be("20");

        var second = await backfill.RunAsync();
        second.CandidatesFound.Should().Be(1, "solo CREDITO sigue sin mapeo");
        second.RowsUpdated.Should().Be(0);
        saveScope.SavedTenants.Should().HaveCount(2, "la segunda corrida no escribe nada");
    }

    private ErpDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var accessor = new HttpContextAccessor();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(interceptors)
            .Options;
        return new ErpDbContext(
            options,
            new CurrentTenantService(accessor),
            new NoOpPublisher(),
            new CurrentCompanyService(accessor)
        );
    }

    private async Task<CompanyScope> CreateCompanyScopeAsync(
        ErpDbContext db,
        Tenant tenant,
        Company company,
        BusinessPartner supplier,
        PaymentTerm paymentTerm,
        Item item,
        int ordinal
    )
    {
        var branch = Branch.Create(
            tenant.Id,
            "Main",
            "1 Main Street",
            $"BR-{ordinal}",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _actorId,
            companyId: company.Id
        );
        var warehouse = Warehouse.Create(
            tenant.Id,
            branch.Id,
            "Main warehouse",
            $"WH-{ordinal}",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _actorId,
            company.Id,
            isMain: true
        );
        var cajaGeneral = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create("1.1.01.001"),
            "Caja general",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            _actorId
        );
        var alternate = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create("1.1.01.002"),
            "Caja manual",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            _actorId
        );
        var register = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            $"CASH-{ordinal}",
            "Unconfigured cash register",
            _actorId
        );
        var configuredRegister = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            $"CASH-MANUAL-{ordinal}",
            "Manually configured cash register",
            _actorId
        );
        configuredRegister.SetAccountingAccount(alternate.Id, _actorId);
        CashRegister? concurrentRegister = ordinal == 1
            ? CashRegister.Create(
                tenant.Id,
                company.Id,
                branch.Id,
                $"CASH-RACE-{ordinal}",
                "Account assigned after initial scan",
                _actorId
            )
            : null;
        db.Branches.Add(branch);
        db.Warehouses.Add(warehouse);
        db.Accounts.AddRange(cajaGeneral, alternate);
        db.CashRegisters.AddRange(register, configuredRegister);
        if (concurrentRegister is not null) db.CashRegisters.Add(concurrentRegister);
        await db.SaveChangesAsync();

        var invoice = PurchaseInvoice.CreateDraft(
            tenant.Id,
            company.Id,
            branch.Id,
            supplier.Id,
            supplier.Name.LegalName,
            supplier.Identification.Number,
            docTypeCode: "01",
            invoiceNumber: $"001-001-{ordinal:000000000}",
            issueDate: new DateOnly(2026, 9, 1),
            createdBy: _actorId,
            paymentTerm.Id,
            paymentTerm.Name,
            paymentTerm.Installments,
            paymentTerm.DaysBetweenInstallments,
            globalWarehouseId: warehouse.Id
        );
        var invoiceLine = PurchaseInvoiceDetail.Create(
            invoice.Id,
            tenant.Id,
            "Item for return scope test",
            quantity: 2m,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: item.Id,
            warehouseId: warehouse.Id
        );
        invoice.ReplaceLines([invoiceLine], _actorId);
        invoice.Confirm(_actorId);
        invoice.ClearDomainEvents();
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();

        var original = invoice.Lines.Single();
        var purchaseReturn = PurchaseReturn.CreateDraft(
            tenant.Id,
            company.Id,
            branch.Id,
            invoice.Id,
            supplier.Id,
            "Scope test return",
            [new PurchaseReturn.DraftLineInput(original.Id, item.Id, 1m, warehouse.Id)],
            _actorId,
            Guid.NewGuid(),
            $"draft-{ordinal}"
        );
        var snapshots = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [original.Id] = new(
                original.Quantity,
                original.LineSubtotal,
                original.DiscountAmount,
                original.VatAmount,
                original.IceAmount,
                original.VatCode,
                original.VatRate,
                original.IceCode,
                original.IceRate,
                original.LandedUnitCost,
                Array.Empty<PurchaseReturn.OriginalLineTaxSnapshot>()
            ),
        };
        purchaseReturn.Authorize(
            $"{ordinal:00000000}",
            snapshots,
            balanceDueBeforeApplication: 100000m,
            invoice.CurrencyCode,
            hasIssuedRetention: false,
            _actorId,
            Guid.NewGuid(),
            $"authorize-{ordinal}"
        );
        purchaseReturn.ClearDomainEvents();
        db.PurchaseReturns.Add(purchaseReturn);
        await db.SaveChangesAsync();

        var periodDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var period = AccountingPeriod.Create(
            tenant.Id,
            company.Id,
            periodDate.Year,
            periodDate.Month,
            new DateOnly(periodDate.Year, periodDate.Month, 1),
            new DateOnly(periodDate.Year, periodDate.Month, DateTime.DaysInMonth(periodDate.Year, periodDate.Month)),
            _actorId
        );
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();

        return new CompanyScope(
            tenant.Id,
            company.Id,
            branch.Id,
            cajaGeneral.Id,
            alternate.Id,
            register.Id,
            configuredRegister.Id,
            concurrentRegister?.Id,
            invoice.Id,
            purchaseReturn.Id,
            period.Id,
            periodDate
        );
    }

    private static Item CreateItem(Guid tenantId, Guid itemTypeId, string sku) => Item.Create(
        tenantId,
        sku,
        sku,
        sku,
        itemTypeId,
        "UNIT",
        taxConfig: ItemTaxConfig.Create("10", "10"),
        saleConfig: ItemSaleConfig.Create(isForSale: true),
        stockConfig: ItemStockConfig.Create(tracksStock: true),
        createdBy: Guid.NewGuid()
    );

    private sealed record CompanyScope(
        Guid TenantId,
        Guid CompanyId,
        Guid BranchId,
        Guid CajaGeneralAccountId,
        Guid AlternateAccountId,
        Guid CashRegisterId,
        Guid ConfiguredCashRegisterId,
        Guid? ConcurrentCashRegisterId,
        Guid PurchaseInvoiceId,
        Guid PurchaseReturnId,
        Guid AccountingPeriodId,
        DateOnly PeriodDate
    );

    private sealed class PersistingPostingEngine(
        ErpDbContext db,
        IReadOnlyList<CompanyScope> scopes,
        Guid actorId,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    ) : IPostingEngine
    {
        public List<PostingFact> Facts { get; } = [];

        public Task<Result<PostingOutcomeDto>> PostAsync(
            PostingFact fact,
            CancellationToken cancellationToken = default
        )
        {
            Facts.Add(fact);
            var scope = scopes.Single(item => item.CompanyId == fact.CompanyId);
            if (scope.TenantId != fact.TenantId
                || currentTenant.TenantId != fact.TenantId
                || currentCompany.CompanyId != fact.CompanyId)
                throw new InvalidOperationException("Posting fact escaped its tenant/company scope.");
            var entry = JournalEntry.Create(
                fact.TenantId,
                fact.CompanyId,
                fact.EntryDate,
                scope.AccountingPeriodId,
                fact.EntryDate.Year,
                fact.SourceModule,
                fact.FactType,
                fact.SourceEventId,
                "Scope integration posting",
                actorId
            );
            db.JournalEntries.Add(entry);
            return Task.FromResult(Result<PostingOutcomeDto>.Success(
                new PostingOutcomeDto(entry.Id, PostingOutcomeStatus.Created)
            ));
        }

        public Task<bool> IsAmountKindConfiguredAsync(
            Guid tenantId,
            Guid companyId,
            string sourceModule,
            string factType,
            PostingAmountKind amountKind,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(true);
    }

    private sealed class AssignAccountAfterInitialCashScanInterceptor(
        string connectionString,
        Guid cashRegisterId,
        Guid accountingAccountId
    ) : DbCommandInterceptor
    {
        private bool _updated;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default
        )
        {
            if (!_updated
                && command.CommandText.Contains("cash_registers", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("accounting_account_id", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("IS NULL", StringComparison.OrdinalIgnoreCase))
            {
                _updated = true;
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var update = new NpgsqlCommand(
                    "UPDATE cash_registers SET accounting_account_id = @accountId WHERE id = @registerId",
                    connection
                );
                update.Parameters.AddWithValue("accountId", accountingAccountId);
                update.Parameters.AddWithValue("registerId", cashRegisterId);
                (await update.ExecuteNonQueryAsync(cancellationToken)).Should().Be(1);
            }

            return result;
        }
    }

    /// <summary>
    /// Falla si un SaveChanges escribe PaymentMethods fuera del tenant del JobExecutionContext
    /// activo, o de más de un tenant a la vez; registra el tenant de cada SaveChanges con cambios.
    /// </summary>
    private sealed class PaymentMethodSaveScopeInterceptor : SaveChangesInterceptor
    {
        public List<Guid> SavedTenants { get; } = [];

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            var writtenTenants = eventData.Context!.ChangeTracker
                .Entries<PaymentMethod>()
                .Where(entry => entry.State is not EntityState.Unchanged and not EntityState.Detached)
                .Select(entry => entry.Entity.TenantId)
                .Distinct()
                .ToList();
            if (writtenTenants.Count > 0)
            {
                writtenTenants.Should().ContainSingle("un SaveChanges nunca mezcla tenants");
                writtenTenants[0].Should().Be(JobTenantContext.Current, "solo se escribe el tenant del scope activo");
                SavedTenants.Add(writtenTenants[0]);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        ) where TNotification : INotification => Task.CompletedTask;
    }
}