using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Tests.Seeding;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BranchEntity = ERP.Domain.Branches.Entities.Branch;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-7B — PostgreSQL 16 + migraciones completas, Posting Engine REAL (idempotency advisory lock,
/// JournalEntrySequence, índice único de asiento) y comando por MediatR. Lotes Completed sembrados
/// como los dejan IL-4/5/6 (backfill de lotes históricos). Verifica: un asiento correcto por lote
/// para inventario/CxC/CxP, intentos concurrentes → un solo asiento, reejecución idempotente,
/// PERIOD_NOT_OPEN y regla faltante → Failed sin asiento y sin tocar la carga operativa, reintento,
/// reverso rechazado.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PostOpeningBalancePostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    private static readonly DateOnly Cutoff = new(2026, 8, 31);
    private static long _nextTaxNumber = 1790097000;
    private readonly ServiceProvider _services;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _branch;
    private Guid _customer;
    private Guid _supplier;
    private Guid _itemType;
    private Guid _warehouse;

    public PostOpeningBalancePostgreSqlTests(InitialLoadPostgresFixture postgres)
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(() => _tenant);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(x => x.CompanyId).Returns(() => _company);
        company.SetupGet(x => x.HasCompanyContext).Returns(() => _company != Guid.Empty);
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(() => _tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(() => _company);
        ctx.SetupGet(x => x.HasTenant).Returns(() => _tenant != Guid.Empty);
        ctx.SetupGet(x => x.HasCompany).Returns(() => _company != Guid.Empty);
        ctx.SetupGet(x => x.UserId).Returns(_user);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(PostOpeningBalanceCommand).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<PostOpeningBalanceCommand>(ServiceLifetime.Transient);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(ctx.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il7b@test" && x.FullName == "IL7B"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton<ICompanyClock>(new AlwaysTodayCompanyClock());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IOpeningBalancePostingRepository, OpeningBalancePostingRepository>();
        services.AddScoped<IOpeningBalanceConstraintsReader, OpeningBalanceConstraintsReader>();
        services.AddScoped<IOpeningBalanceSourceReader, OpeningBalanceSourceReader>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddScoped<PostingPreflight>();
        services.AddScoped<OpeningBalancePostingPreflight>();
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL7B", "il7b-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL7B S.A.", createdBy: _user);
        company.SetOpeningBalanceDate(Cutoff, new OpeningBalanceDateConstraints(false, []), _user);
        _company = company.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var branch = BranchEntity.Create(tenantId: _tenant, name: "Matriz", address: "Av. 1", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: true, createdBy: _user, companyId: _company);
        db.Branches.Add(branch);
        // Tercero real con roles Cliente y Proveedor (FK de CxC/CxP a master_business_partners),
        // mismo patrón que ValidateInitialReceivables/PayablesPostgreSqlTests.
        var partner = BusinessPartner.Create(_tenant, "04", "1790016919001", null, "Tercero IL7B S.A.", _user);
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, partner.Id, RoleType.Customer, _user));
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, partner.Id, RoleType.Supplier, _user));
        await db.SaveChangesAsync();
        (_branch, _customer, _supplier) = (branch.Id, partner.Id, partner.Id);
        // Kardex: el trigger a1_movement_product_scope exige un Item Product de esta misma
        // tenant + company (mismo patrón que ConfirmInitialStockAtomicPostgreSqlTests).
        var warehouse = Warehouse.Create(_tenant, _branch, "Bodega Principal", "BOD-01", null, null, null, null, null,
            null, null, null, null, _user, _company, isMain: true);
        var itemType = ItemTypeDefinition.Create(_tenant, "MERCH", "Mercadería", 1, _user);
        db.Warehouses.Add(warehouse);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();
        (_warehouse, _itemType) = (warehouse.Id, itemType.Id);
        // Plan de cuentas, período 2026 abierto y reglas (incluidas InitialLoad/*) por el bootstrap real.
        await new AccountingBootstrapStep(db, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance)
            .ExecuteAsync(new CompanyBootstrapContext(_tenant, _company, _user));
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    private ImportBatch CompletedBatch(ImportType type, int rows)
    {
        var batch = ImportBatch.Create(_tenant, _company, type, _user);
        batch.AttachFile("x.xlsx", "x.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        batch.BeginValidating(_user);
        batch.CompleteValidation(rows, rows, 0, 0, _user);
        batch.BeginConfirming(_user);
        batch.CompleteConfirmation(rows, anyRowsFailed: false, _user);
        return batch;
    }

    private async Task<Guid> SeedAsync(Action<ErpDbContext, ImportBatch> data, ImportType type)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = CompletedBatch(type, 1);
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        data(db, batch);
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private Task<Guid> ReceivablesAsync(decimal amount) =>
        SeedAsync((db, b) => db.SalesReceivables.Add(SalesReceivable.CreateInitialBalance(_tenant, _company, _branch,
            _customer, "FAC-" + Guid.NewGuid().ToString("N")[..6], Cutoff.AddDays(-5), Cutoff.AddDays(25), amount, b.Id, _user)),
            ImportType.InitialReceivables);

    private Task<Guid> PayablesAsync(decimal amount) =>
        SeedAsync((db, b) => db.AccountsPayables.Add(AccountsPayable.CreateInitialBalance(_tenant, _company, _branch,
            _supplier, "01", "001-001-" + Random.Shared.Next(1, 999999999).ToString("D9"), Cutoff.AddDays(-30),
            Cutoff.AddDays(10), Cutoff, amount, b.Id, Guid.NewGuid(), _user)), ImportType.InitialPayables);

    private Task<Guid> StockAsync(decimal quantity, decimal unitCost) =>
        SeedAsync((db, b) =>
        {
            var sku = "IL7B-" + Guid.NewGuid().ToString("N")[..8];
            var item = Item.Create(_tenant, sku, "Producto " + sku, "Producto " + sku, _itemType, "UNIT",
                ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: "4"), ItemSaleConfig.Create(isForSale: true),
                ItemStockConfig.Create(stockControlEnabled: true), _user, companyId: _company);
            db.Set<Item>().Add(item);
            var document = Guid.NewGuid();
            var row = ImportBatchRow.Create(_tenant, _company, b.Id, 1, "{}", _user);
            row.SetParsedData("{}", false, _user);
            row.MarkImported(document, _user);
            db.ImportBatchRows.Add(row);
            db.StockMovements.Add(StockMovement.Create(_tenant, _branch, item.Id, _warehouse,
                StockMovementType.InitialBalance, quantity, "UND", 0m, 1, unitCost, quantity * unitCost, Cutoff, "AJ-1",
                document, "StockAdjustment", _user, _company, unitCost: unitCost));
        }, ImportType.InitialStock);

    private async Task<Result<OpeningBalancePostingDto>> PostAsync(Guid batch)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new PostOpeningBalanceCommand(batch));
    }

    private async Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    private Task<int> EntriesForAsync(Guid batch) =>
        QueryAsync(db => db.JournalEntries.CountAsync(e => e.SourceModule == "InitialLoad" && e.SourceEventId == batch));

    [Fact]
    public async Task Las_tres_aperturas_generan_un_asiento_balanceado_por_lote_a_la_fecha_de_apertura()
    {
        var stock = await StockAsync(3m, 1.3335m);
        var receivables = await ReceivablesAsync(150m);
        var payables = await PayablesAsync(80.55m);

        foreach (var (batch, factType, debit, credit, amount) in new[]
                 {
                     (stock, "OpeningInventory", "1.1.04.001", "3.1.04.001", 4.00m),
                     (receivables, "OpeningReceivables", "1.1.03.001", "3.1.04.001", 150m),
                     (payables, "OpeningPayables", "3.1.04.001", "2.1.01.001", 80.55m),
                 })
        {
            var result = await PostAsync(batch);
            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value!.Status.Should().Be(OpeningBalancePostingStatus.Posted);

            var entry = await QueryAsync(db => db.JournalEntries.Include(e => e.Lines).AsNoTracking()
                .SingleAsync(e => e.SourceModule == "InitialLoad" && e.SourceEventId == batch));
            entry.Id.Should().Be(result.Value.JournalEntryId!.Value);
            entry.SourceEventType.Should().Be(factType);
            entry.EntryDate.Should().Be(Cutoff);
            entry.Status.Should().Be(JournalEntryStatus.Posted);
            entry.EntryNumber.Should().BeGreaterThan(0);
            var codes = await QueryAsync(db => db.Accounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Code.Value));
            entry.Lines.Select(l => (codes[l.AccountId], l.Debit, l.Credit)).Should().BeEquivalentTo(new[]
            {
                (debit, amount, 0m),
                (credit, 0m, amount),
            });
        }
        (await QueryAsync(db => db.JournalEntries.Where(e => e.SourceModule == "InitialLoad")
                .Select(e => e.EntryNumber).Distinct().CountAsync()))
            .Should().Be(3, "numeración correlativa propia de JournalEntrySequence, sin repetir");
    }

    [Fact]
    public async Task Intentos_concurrentes_crean_un_solo_asiento_y_todos_devuelven_el_mismo()
    {
        var batch = await ReceivablesAsync(75m);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => PostAsync(batch))));

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Value!.JournalEntryId).Distinct().Should().ContainSingle();
        results.Count(r => !r.Value!.AlreadyPosted).Should().Be(1);
        (await EntriesForAsync(batch)).Should().Be(1);
        var state = await QueryAsync(db => db.OpeningBalancePostings.AsNoTracking().SingleAsync(p => p.ImportBatchId == batch));
        state.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        state.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Reejecucion_es_idempotente_sin_nuevo_asiento_ni_numero()
    {
        var batch = await PayablesAsync(20m);
        var first = await PostAsync(batch);
        var sequences = await QueryAsync(db => db.Set<ERP.Domain.Modules.Accounting.Entities.JournalEntrySequence>()
            .AsNoTracking().Select(s => s.LastNumber).ToListAsync());

        var second = await PostAsync(batch);

        second.Value!.AlreadyPosted.Should().BeTrue();
        second.Value.JournalEntryId.Should().Be(first.Value!.JournalEntryId);
        (await EntriesForAsync(batch)).Should().Be(1);
        (await QueryAsync(db => db.Set<ERP.Domain.Modules.Accounting.Entities.JournalEntrySequence>()
            .AsNoTracking().Select(s => s.LastNumber).ToListAsync())).Should().Equal(sequences);
    }

    [Fact]
    public async Task Periodo_cerrado_deja_Failed_sin_asiento_y_sin_tocar_la_carga_operativa()
    {
        var batch = await ReceivablesAsync(30m);
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.AccountingPeriods.SingleAsync(p => p.CompanyId == _company))
                .Close(_user, new JournalEntryClosureReadiness(false, false, false));
            await db.SaveChangesAsync();
        }

        var result = await PostAsync(batch);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        (await EntriesForAsync(batch)).Should().Be(0);
        var state = await QueryAsync(db => db.OpeningBalancePostings.AsNoTracking().SingleAsync(p => p.ImportBatchId == batch));
        state.Status.Should().Be(OpeningBalancePostingStatus.Failed);
        state.ErrorCode.Should().Be("PERIOD_NOT_OPEN");
        (await QueryAsync(db => db.SalesReceivables.CountAsync(r => r.ImportBatchId == batch))).Should().Be(1);
        (await QueryAsync(db => db.ImportBatches.AsNoTracking().SingleAsync(b => b.Id == batch))).Status
            .Should().Be(ImportStatus.Completed);
    }

    [Fact]
    public async Task Regla_faltante_deja_Failed_y_el_reintento_contabiliza_una_sola_vez()
    {
        var batch = await StockAsync(2m, 5m);
        async Task ToggleRuleAsync(bool enable)
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PostingRules.SingleAsync(r => r.CompanyId == _company && r.SourceModule == "InitialLoad"
                && r.FactType == "OpeningInventory");
            if (enable) rule.Enable(_user); else rule.Disable(_user);
            await db.SaveChangesAsync();
        }

        await ToggleRuleAsync(enable: false);
        (await PostAsync(batch)).Code.Should().Be("RULE_NOT_FOUND");
        (await EntriesForAsync(batch)).Should().Be(0);

        await ToggleRuleAsync(enable: true);
        var retried = await PostAsync(batch);

        retried.IsSuccess.Should().BeTrue(retried.Error);
        (await EntriesForAsync(batch)).Should().Be(1);
        var state = await QueryAsync(db => db.OpeningBalancePostings.AsNoTracking().SingleAsync(p => p.ImportBatchId == batch));
        state.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        state.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task Reverso_del_asiento_de_apertura_se_rechaza_y_todo_queda_igual()
    {
        var batch = await PayablesAsync(40m);
        var posted = await PostAsync(batch);

        await using var scope = _services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ReverseJournalEntryCommand(posted.Value!.JournalEntryId!.Value, "corrección"));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ReverseJournalEntryCommandHandler.OpeningReversalNotAllowedCode);
        (await EntriesForAsync(batch)).Should().Be(1);
        (await QueryAsync(db => db.JournalEntries.AsNoTracking().CountAsync(e => e.OriginalJournalEntryId != null
            && e.CompanyId == _company))).Should().Be(0);
        (await QueryAsync(db => db.OpeningBalancePostings.AsNoTracking().SingleAsync(p => p.ImportBatchId == batch)))
            .Status.Should().Be(OpeningBalancePostingStatus.Posted);
    }
}
