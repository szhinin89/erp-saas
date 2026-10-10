using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Application.Modules.InitialLoad.UseCases.OpeningBalanceDate;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
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
/// IL-8A — ASI de apertura sobre PostgreSQL 16 + migraciones completas, Posting Engine REAL
/// (idempotency advisory lock, JournalEntrySequence) y comandos por MediatR. La cuenta puente se
/// carga con aperturas IL-7B reales (CxC 150 − CxP 50 = 100 acreedor). Cubre: publicación con
/// partida doble que deja la puente en 0, puente ≠ 0, descuadre, cuentas de control
/// Inventario/CxC/CxP (resueltas desde las reglas), cuenta inexistente/inactiva/sin movimiento/de
/// otra empresa, período cerrado, fecha nula, regla ASI con línea fija o deshabilitada,
/// idempotencia y concurrencia sin duplicar asiento ni secuencia, aislamiento por empresa,
/// fecha de apertura inmutable tras publicar y reverso genérico rechazado.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class PublishOpeningJournalEntryPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    private static readonly DateOnly Cutoff = new(2026, 8, 31);
    private static long _nextTaxNumber = 1790099000;
    private readonly ServiceProvider _services;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _branch;
    private Guid _partner;

    public PublishOpeningJournalEntryPostgreSqlTests(InitialLoadPostgresFixture postgres)
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
        var guard = new Mock<ICompanyAccessGuard>();
        guard.Setup(x => x.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result<CompanyAccessContext>.Success(
                new CompanyAccessContext(_user, _tenant, _company, "Admin", true, true)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(PublishOpeningJournalEntryCommand).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<PublishOpeningJournalEntryCommand>(ServiceLifetime.Transient);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(ctx.Object);
        services.AddSingleton(guard.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il8a@test" && x.FullName == "IL8A"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton<ICompanyClock>(new AlwaysTodayCompanyClock());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IOpeningBalancePostingRepository, OpeningBalancePostingRepository>();
        services.AddScoped<IOpeningJournalEntryPostingRepository, OpeningJournalEntryPostingRepository>();
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
        services.AddScoped<ERP.Domain.Access.Interfaces.IAccessRepository, AccessRepository>();
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL8A", "il8a-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            var partner = BusinessPartner.Create(_tenant, "04", "1790016919001", null, "Tercero IL8A S.A.", _user);
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, partner.Id, RoleType.Customer, _user));
            db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(_tenant, partner.Id, RoleType.Supplier, _user));
            await db.SaveChangesAsync();
            _partner = partner.Id;
        }
        (_company, _branch) = await NewCompanyAsync();
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    /// <summary>Empresa con fecha de apertura, sucursal, plan de cuentas, período 2026 y reglas (bootstrap real).</summary>
    private async Task<(Guid Company, Guid Branch)> NewCompanyAsync()
    {
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL8A S.A.", createdBy: _user);
        company.SetOpeningBalanceDate(Cutoff, new OpeningBalanceDateConstraints(false, []), _user);
        var previous = _company;
        _company = company.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var branch = BranchEntity.Create(tenantId: _tenant, name: "Matriz", address: "Av. 1", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: true, createdBy: _user, companyId: company.Id);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        await new AccountingBootstrapStep(db, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance)
            .ExecuteAsync(new CompanyBootstrapContext(_tenant, company.Id, _user));
        _company = previous == Guid.Empty ? company.Id : previous;
        return (company.Id, branch.Id);
    }

    private ImportBatch CompletedBatch(ImportType type)
    {
        var batch = ImportBatch.Create(_tenant, _company, type, _user);
        batch.AttachFile("x.xlsx", "x.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        batch.BeginValidating(_user);
        batch.CompleteValidation(1, 1, 0, 0, _user);
        batch.BeginConfirming(_user);
        batch.CompleteConfirmation(1, anyRowsFailed: false, _user);
        return batch;
    }

    /// <summary>Aperturas IL-7B reales: CxC 150 y CxP 50 → cuenta puente 100 acreedora.</summary>
    private async Task LoadBridgeAsync(decimal receivables = 150m, decimal payables = 50m)
    {
        Guid receivablesBatch, payablesBatch;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var r = CompletedBatch(ImportType.InitialReceivables);
            var p = CompletedBatch(ImportType.InitialPayables);
            db.ImportBatches.AddRange(r, p);
            await db.SaveChangesAsync();
            db.SalesReceivables.Add(SalesReceivable.CreateInitialBalance(_tenant, _company, _branch, _partner,
                "FAC-" + Guid.NewGuid().ToString("N")[..6], Cutoff.AddDays(-5), Cutoff.AddDays(25), receivables, r.Id, _user));
            db.AccountsPayables.Add(AccountsPayable.CreateInitialBalance(_tenant, _company, _branch, _partner, "01",
                "001-001-" + Random.Shared.Next(1, 999999999).ToString("D9"), Cutoff.AddDays(-30), Cutoff.AddDays(10),
                Cutoff, payables, p.Id, Guid.NewGuid(), _user));
            await db.SaveChangesAsync();
            (receivablesBatch, payablesBatch) = (r.Id, p.Id);
        }
        foreach (var batch in new[] { receivablesBatch, payablesBatch })
        {
            await using var scope = _services.CreateAsyncScope();
            var posted = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new PostOpeningBalanceCommand(batch));
            posted.IsSuccess.Should().BeTrue(posted.Error);
        }
    }

    private async Task<Result<T>> SendAsync<T>(IRequest<Result<T>> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private Task<Result<OpeningJournalEntryPostingDto>> PublishAsync(params OpeningJournalEntryLineInput[] lines) =>
        SendAsync(new PublishOpeningJournalEntryCommand(lines));

    private async Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    private Task<Guid> AccountAsync(string code) => AccountOfAsync(_company, code);

    private Task<Guid> AccountOfAsync(Guid companyId, string code) =>
        QueryAsync(async db => (await db.Accounts.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.TenantId == _tenant && a.CompanyId == companyId).ToListAsync())
            .Single(a => a.Code.Value == code).Id);

    /// <summary>Cuenta del lado indicado en la regla InitialLoad/FactType (nunca por código).</summary>
    private Task<Guid> RuleAccountAsync(string factType, AccountNature nature) =>
        QueryAsync(async db => (await db.PostingRules.Include(r => r.Lines)
                .SingleAsync(r => r.CompanyId == _company && r.SourceModule == "InitialLoad" && r.FactType == factType))
            .Lines.Single(l => l.Nature == nature).AccountId);

    private Task<Guid> BridgeAsync() => RuleAccountAsync("OpeningReceivables", AccountNature.Credit);

    private Task<int> AsiEntriesAsync() =>
        QueryAsync(db => db.JournalEntries.CountAsync(e => e.CompanyId == _company && e.SourceModule == "InitialLoad"
            && e.SourceEventType == "ASI"));

    private Task<List<int>> SequencesAsync() =>
        QueryAsync(db => db.Set<JournalEntrySequence>().AsNoTracking().Where(s => s.CompanyId == _company)
            .Select(s => s.LastNumber).ToListAsync());

    private async Task<decimal> BridgeBalanceAsync()
    {
        var bridge = await BridgeAsync();
        return await QueryAsync(async db =>
        {
            var lines = await db.JournalEntries.Where(e => e.CompanyId == _company && e.Status == JournalEntryStatus.Posted)
                .SelectMany(e => e.Lines).Where(l => l.AccountId == bridge).ToListAsync();
            return lines.Sum(l => l.Credit - l.Debit);
        });
    }

    /// <summary>Versión vigente del ASI de la empresa (null si no hay).</summary>
    private Task<OpeningJournalEntryPosting?> StateAsync() =>
        QueryAsync(db => db.OpeningJournalEntryPostings.AsNoTracking()
            .SingleOrDefaultAsync(p => p.CompanyId == _company && p.IsCurrent));

    private Task<List<OpeningJournalEntryPosting>> VersionsAsync() =>
        QueryAsync(db => db.OpeningJournalEntryPostings.AsNoTracking().Where(p => p.CompanyId == _company)
            .OrderBy(p => p.Version).ToListAsync());

    private static OpeningJournalEntryLineInput Debit(Guid account, decimal amount, string? description = null) =>
        new(account, amount, 0m, description);

    private static OpeningJournalEntryLineInput Credit(Guid account, decimal amount, string? description = null) =>
        new(account, 0m, amount, description);

    private async Task<OpeningJournalEntryLineInput[]> ValidLinesAsync() =>
    [
        Debit(await BridgeAsync(), 100m, "Reclasificación de saldos de apertura"),
        Credit(await AccountAsync("3.1.01.001"), 60m, "Capital"),
        Credit(await AccountAsync("3.1.02.001"), 40m),
    ];

    private async Task ShouldFailWithoutEntryAsync(Result<OpeningJournalEntryPostingDto> result, string code)
    {
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(code, result.Error);
        (await AsiEntriesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Publica_un_ASI_balanceado_a_la_fecha_de_apertura_y_deja_la_puente_en_cero()
    {
        await LoadBridgeAsync();
        (await BridgeBalanceAsync()).Should().Be(100m);
        var sequencesBefore = (await SequencesAsync()).Sum();

        var result = await PublishAsync(await ValidLinesAsync());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        result.Value.AlreadyPosted.Should().BeFalse();
        result.Value.EntryDate.Should().Be(Cutoff);
        result.Value.TotalAmount.Should().Be(100m);
        result.Value.LineCount.Should().Be(3);
        var entry = await QueryAsync(db => db.JournalEntries.Include(e => e.Lines).AsNoTracking()
            .SingleAsync(e => e.Id == result.Value.JournalEntryId));
        entry.SourceModule.Should().Be("InitialLoad");
        entry.SourceEventType.Should().Be("ASI");
        entry.SourceEventId.Should().Be(result.Value.Id, "SourceEventId = id del registro persistente del ASI");
        entry.EntryDate.Should().Be(Cutoff);
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.EntryNumber.Should().BeGreaterThan(0);
        entry.Lines.Sum(l => l.Debit).Should().Be(100m);
        entry.Lines.Sum(l => l.Credit).Should().Be(100m);
        var codes = await QueryAsync(db => db.Accounts.AsNoTracking().Where(a => a.CompanyId == _company)
            .ToDictionaryAsync(a => a.Id, a => a.Code.Value));
        entry.Lines.Select(l => (codes[l.AccountId], l.Debit, l.Credit, l.Description)).Should().BeEquivalentTo(new[]
        {
            ("3.1.04.001", 100m, 0m, "Reclasificación de saldos de apertura"),
            ("3.1.01.001", 0m, 60m, "Capital"),
            ("3.1.02.001", 0m, 40m, $"InitialLoad — ASI — {result.Value.Id}"),
        });
        (await BridgeBalanceAsync()).Should().Be(0m);
        (await SequencesAsync()).Sum().Should().Be(sequencesBefore + 1, "un solo número de JournalEntrySequence");
        var state = await StateAsync();
        state!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        state.JournalEntryId.Should().Be(entry.Id);
        state.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Puente_que_no_queda_en_cero_bloquea_sin_asiento_ni_numero_y_el_reintento_publica()
    {
        await LoadBridgeAsync();
        var sequences = await SequencesAsync();

        var result = await PublishAsync(Debit(await BridgeAsync(), 90m), Credit(await AccountAsync("3.1.01.001"), 90m));

        await ShouldFailWithoutEntryAsync(result, PublishOpeningJournalEntryCommandHandler.BridgeNotClearedCode);
        result.Error.Should().Contain("10.00");
        (await SequencesAsync()).Should().Equal(sequences);
        var failed = await StateAsync();
        failed!.Status.Should().Be(OpeningBalancePostingStatus.Failed);
        failed.ErrorCode.Should().Be(PublishOpeningJournalEntryCommandHandler.BridgeNotClearedCode);

        var retried = await PublishAsync(await ValidLinesAsync());

        retried.IsSuccess.Should().BeTrue(retried.Error);
        retried.Value!.Id.Should().Be(failed.Id, "el reintento conserva la identidad (SourceEventId) del ASI");
        retried.Value.Version.Should().Be(1);
        (await VersionsAsync()).Should().ContainSingle();
        retried.Value.Attempts.Should().Be(2);
        (await AsiEntriesAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Sin_saldo_en_la_puente_un_asiento_que_la_mueve_tambien_bloquea()
    {
        var result = await PublishAsync(Debit(await AccountAsync("3.1.02.001"), 5m), Credit(await BridgeAsync(), 5m));

        await ShouldFailWithoutEntryAsync(result, PublishOpeningJournalEntryCommandHandler.BridgeNotClearedCode);
    }

    [Fact]
    public async Task Descuadre_de_partida_doble_bloquea()
    {
        await LoadBridgeAsync();

        var result = await PublishAsync(Debit(await BridgeAsync(), 100m), Credit(await AccountAsync("3.1.01.001"), 99.99m));

        await ShouldFailWithoutEntryAsync(result, "VALIDATION_FAILED");
    }

    [Theory]
    [InlineData("OpeningInventory", AccountNature.Debit)]
    [InlineData("OpeningReceivables", AccountNature.Debit)]
    [InlineData("OpeningPayables", AccountNature.Credit)]
    public async Task Cuenta_de_control_resuelta_desde_la_regla_bloquea(string factType, AccountNature controlSide)
    {
        await LoadBridgeAsync();
        var control = await RuleAccountAsync(factType, controlSide);

        var result = await PublishAsync(Debit(await BridgeAsync(), 100m), Credit(control, 100m));

        await ShouldFailWithoutEntryAsync(result, PublishOpeningJournalEntryCommandHandler.ControlAccountBlockedCode);
    }

    [Fact]
    public async Task Cuenta_de_control_personalizada_en_la_regla_tambien_bloquea()
    {
        await LoadBridgeAsync();
        var custom = await AccountAsync("3.1.03.001");
        await using (var scope = _services.CreateAsyncScope())
        {
            // Una empresa que reasigna la cuenta de control de inventario en su regla: el bloqueo
            // sigue a la regla, nunca a un código fijo.
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PostingRules.Include(r => r.Lines).SingleAsync(r => r.CompanyId == _company
                && r.SourceModule == "InitialLoad" && r.FactType == "OpeningInventory");
            var line = rule.Lines.Single(l => l.Nature == AccountNature.Debit);
            rule.RemoveLine(line.AccountId, line.Nature, line.AmountKind);
            rule.AddLine(custom, AccountNature.Debit, PostingAmountKind.GrandTotal);
            await db.SaveChangesAsync();
        }

        var result = await PublishAsync(Debit(await BridgeAsync(), 100m), Credit(custom, 100m));

        await ShouldFailWithoutEntryAsync(result, PublishOpeningJournalEntryCommandHandler.ControlAccountBlockedCode);
        result.Error.Should().Contain("Inventario");
    }

    [Fact]
    public async Task Cuenta_inexistente_inactiva_sin_movimiento_o_de_otra_empresa_bloquea()
    {
        await LoadBridgeAsync();
        var bridge = await BridgeAsync();
        var inactive = await AccountAsync("3.1.03.001");
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.Accounts.SingleAsync(a => a.Id == inactive)).Disable(_user);
            await db.SaveChangesAsync();
        }
        var group = await AccountAsync("3.1.01");
        var (otherCompany, _) = await NewCompanyAsync();
        var foreign = await AccountOfAsync(otherCompany, "3.1.01.001");

        foreach (var account in new[] { Guid.NewGuid(), inactive, group, foreign })
            await ShouldFailWithoutEntryAsync(
                await PublishAsync(Debit(bridge, 100m), Credit(account, 100m)), "POSTING_ACCOUNT_INVALID");
    }

    [Fact]
    public async Task Periodo_cerrado_bloquea()
    {
        await LoadBridgeAsync();
        var lines = await ValidLinesAsync();
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.AccountingPeriods.SingleAsync(p => p.CompanyId == _company))
                .Close(_user, new JournalEntryClosureReadiness(false, false, false));
            await db.SaveChangesAsync();
        }

        await ShouldFailWithoutEntryAsync(await PublishAsync(lines), "PERIOD_NOT_OPEN");
    }

    [Fact]
    public async Task Periodo_inexistente_bloquea()
    {
        await LoadBridgeAsync();
        var lines = await ValidLinesAsync();
        await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE company SET opening_balance_date = {new DateOnly(2019, 12, 31)} WHERE id = {_company}"));

        await ShouldFailWithoutEntryAsync(await PublishAsync(lines), "PERIOD_NOT_OPEN");
    }

    [Fact]
    public async Task Fecha_de_apertura_nula_bloquea_sin_crear_estado()
    {
        var lines = await ValidLinesAsync();
        await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE company SET opening_balance_date = NULL WHERE id = {_company}"));

        await ShouldFailWithoutEntryAsync(await PublishAsync(lines),
            PublishOpeningJournalEntryCommandHandler.OpeningDateMissingCode);
        (await StateAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Regla_ASI_con_linea_fija_o_deshabilitada_bloquea()
    {
        await LoadBridgeAsync();
        var lines = await ValidLinesAsync();
        async Task EditRuleAsync(Action<PostingRule> edit)
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PostingRules.Include(r => r.Lines)
                .SingleAsync(r => r.CompanyId == _company && r.SourceModule == "InitialLoad" && r.FactType == "ASI");
            edit(rule);
            await db.SaveChangesAsync();
        }

        var capital = await AccountAsync("3.1.01.001");
        await EditRuleAsync(r => r.AddLine(capital, AccountNature.Credit, PostingAmountKind.GrandTotal));
        await ShouldFailWithoutEntryAsync(await PublishAsync(lines),
            PublishOpeningJournalEntryCommandHandler.RuleHasFixedLinesCode);

        await EditRuleAsync(r => r.RemoveLine(capital, AccountNature.Credit, PostingAmountKind.GrandTotal));
        await EditRuleAsync(r => r.Disable(_user));
        await ShouldFailWithoutEntryAsync(await PublishAsync(lines), "RULE_NOT_FOUND");
    }

    [Fact]
    public async Task Reejecucion_es_idempotente_sin_nuevo_asiento_ni_numero()
    {
        await LoadBridgeAsync();
        var first = await PublishAsync(await ValidLinesAsync());
        var sequences = await SequencesAsync();

        var second = await PublishAsync(await ValidLinesAsync());

        second.IsSuccess.Should().BeTrue(second.Error);
        second.Value!.AlreadyPosted.Should().BeTrue();
        second.Value.JournalEntryId.Should().Be(first.Value!.JournalEntryId);
        second.Value.Id.Should().Be(first.Value.Id);
        second.Value.Version.Should().Be(1);
        (await AsiEntriesAsync()).Should().Be(1);
        (await SequencesAsync()).Should().Equal(sequences);
        var versions = await VersionsAsync();
        versions.Should().ContainSingle("la reejecución no crea versión nueva");
        versions[0].Attempts.Should().Be(1);
        versions[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Intentos_concurrentes_crean_un_solo_asiento_y_todos_devuelven_el_mismo()
    {
        await LoadBridgeAsync();
        var lines = await ValidLinesAsync();
        var sequencesBefore = (await SequencesAsync()).Sum();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => PublishAsync(lines))));

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Value!.JournalEntryId).Distinct().Should().ContainSingle();
        results.Count(r => !r.Value!.AlreadyPosted).Should().Be(1);
        (await AsiEntriesAsync()).Should().Be(1);
        (await SequencesAsync()).Sum().Should().Be(sequencesBefore + 1);
        var versions = await VersionsAsync();
        versions.Should().ContainSingle("intentos concurrentes de la misma versión no crean otra");
        versions[0].Version.Should().Be(1);
        versions[0].IsCurrent.Should().BeTrue();
        results.Select(r => r.Value!.Id).Distinct().Should().Equal(versions[0].Id);
    }

    /// <summary>
    /// ASI que no toca la cuenta puente (sin cargas confirmadas, saldo 0 → sigue en 0): así la fecha
    /// de apertura solo la gobierna el ASI y no las cargas iniciales confirmadas.
    /// </summary>
    private async Task<OpeningJournalEntryLineInput[]> EquityOnlyLinesAsync() =>
        [Debit(await AccountAsync("3.1.02.001"), 10m), Credit(await AccountAsync("3.1.01.001"), 10m)];

    [Fact]
    public async Task Publicar_y_cambiar_la_fecha_concurrentemente_nunca_deja_un_ASI_con_otra_fecha()
    {
        var lines = await EquityOnlyLinesAsync();

        var publish = Task.Run(() => PublishAsync(lines));
        var change = Task.Run(() => SendAsync(new SetOpeningBalanceDateCommand(Cutoff.AddDays(-1))));
        await Task.WhenAll(publish, change);

        (await publish).IsSuccess.Should().BeTrue((await publish).Error);
        var state = await StateAsync();
        state!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        var date = await QueryAsync(db => db.Companies.AsNoTracking().Where(c => c.Id == _company)
            .Select(c => c.OpeningBalanceDate).SingleAsync());
        date.Should().Be(state.EntryDate, "un ASI publicado fija la fecha de apertura");
        (await change).IsSuccess.Should().Be(date == Cutoff.AddDays(-1), "o cambió antes de publicar, o se rechazó");
    }

    [Fact]
    public async Task La_fecha_de_apertura_es_editable_tras_un_fallo_e_inmutable_tras_publicar()
    {
        (await PublishAsync(Debit(await AccountAsync("3.1.02.001"), 1m), Credit(await BridgeAsync(), 1m)))
            .Code.Should().Be(PublishOpeningJournalEntryCommandHandler.BridgeNotClearedCode);
        var newDate = Cutoff.AddDays(-1);
        (await SendAsync(new SetOpeningBalanceDateCommand(newDate))).IsSuccess
            .Should().BeTrue("un ASI fallido no fija la fecha");

        var published = await PublishAsync(await EquityOnlyLinesAsync());

        published.IsSuccess.Should().BeTrue(published.Error);
        published.Value!.EntryDate.Should().Be(newDate, "el reintento usa la fecha vigente");
        var changed = await SendAsync(new SetOpeningBalanceDateCommand(Cutoff));
        changed.IsSuccess.Should().BeFalse();
        changed.Error.Should().Contain("asiento de apertura");
        (await SendAsync(new SetOpeningBalanceDateCommand(newDate))).IsSuccess.Should().BeTrue("misma fecha: idempotente");
        var after = await SendAsync(new GetOpeningBalanceDateQuery());
        after.Value!.OpeningBalanceDate.Should().Be(newDate);
        after.Value.IsLocked.Should().BeTrue();
        after.Value.LockReason.Should().Contain("ASI");
    }

    [Fact]
    public async Task Aislamiento_por_empresa_el_ASI_de_una_no_afecta_a_otra()
    {
        await LoadBridgeAsync();
        var published = await PublishAsync(await ValidLinesAsync());
        published.IsSuccess.Should().BeTrue(published.Error);
        var companyA = _company;
        var (companyB, branchB) = await NewCompanyAsync();

        _company = companyB;
        _branch = branchB;
        try
        {
            (await StateAsync()).Should().BeNull();
            (await SendAsync(new SetOpeningBalanceDateCommand(Cutoff.AddDays(-1)))).IsSuccess
                .Should().BeTrue("la fecha de B no la bloquea el ASI de A");
            await QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE company SET opening_balance_date = {Cutoff} WHERE id = {companyB}"));
            await LoadBridgeAsync();

            var ownLines = await ValidLinesAsync();
            var resultB = await PublishAsync(ownLines);

            resultB.IsSuccess.Should().BeTrue(resultB.Error);
            resultB.Value!.Id.Should().NotBe(published.Value!.Id);
            resultB.Value.AlreadyPosted.Should().BeFalse();
        }
        finally
        {
            _company = companyA;
        }
        (await QueryAsync(db => db.OpeningJournalEntryPostings.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == _tenant))).Should().Be(2);
    }

    [Fact]
    public async Task Una_sola_version_vigente_por_empresa_lo_garantiza_la_BD()
    {
        (await PublishAsync(await EquityOnlyLinesAsync())).IsSuccess.Should().BeTrue();

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.OpeningJournalEntryPostings.Add(
            OpeningJournalEntryPosting.CreatePending(_tenant, _company, 2, Cutoff, 10m, 2, _user));
        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).WithInnerException<Npgsql.PostgresException>()
            .Which.ConstraintName.Should().Be("ux_opening_journal_entry_postings_company_current");
    }

    /// <summary>
    /// Prepara IL-8B: una versión publicada marcada como reemplazada (el reverso del asiento es IL-8B;
    /// aquí solo se marca el estado) deja publicar una versión NUEVA con Id/SourceEventId distinto,
    /// que el Posting Engine contabiliza como hecho nuevo, conservando la anterior como historial.
    /// </summary>
    [Fact]
    public async Task Reemplazo_de_version_publicada_publica_con_SourceEventId_nuevo_y_conserva_el_historial()
    {
        var v1 = await PublishAsync(await EquityOnlyLinesAsync());
        v1.IsSuccess.Should().BeTrue(v1.Error);
        await using (var scope = _services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IOpeningJournalEntryPostingRepository>();
            (await repo.FindCurrentAsync(_tenant, _company))!.MarkSuperseded(_user);
            await repo.SaveChangesAsync();
        }
        var afterSupersede = await SendAsync(new GetOpeningBalanceDateQuery());
        afterSupersede.Value!.IsLocked.Should().BeTrue("un ASI que llegó a publicarse fija la fecha para siempre");
        var change = await SendAsync(new SetOpeningBalanceDateCommand(Cutoff.AddDays(-1)));
        change.IsSuccess.Should().BeFalse("ni siquiera sin versión vigente");
        change.Error.Should().Contain("asiento de apertura");

        var v2 = await PublishAsync(await EquityOnlyLinesAsync());

        v2.IsSuccess.Should().BeTrue(v2.Error);
        v2.Value!.AlreadyPosted.Should().BeFalse();
        v2.Value.Version.Should().Be(2);
        v2.Value.Id.Should().NotBe(v1.Value!.Id);
        v2.Value.JournalEntryId.Should().NotBe(v1.Value.JournalEntryId!.Value);
        var entries = await QueryAsync(db => db.JournalEntries.AsNoTracking()
            .Where(e => e.CompanyId == _company && e.SourceModule == "InitialLoad" && e.SourceEventType == "ASI")
            .Select(e => new { e.Id, e.SourceEventId }).ToListAsync());
        entries.Select(e => (e.Id, e.SourceEventId)).Should().BeEquivalentTo(new[]
        {
            (v1.Value.JournalEntryId!.Value, v1.Value.Id),
            (v2.Value.JournalEntryId!.Value, v2.Value.Id),
        });
        var versions = await VersionsAsync();
        versions.Select(v => (v.Version, v.IsCurrent, v.Status)).Should().Equal(
            (1, false, OpeningBalancePostingStatus.Posted),
            (2, true, OpeningBalancePostingStatus.Posted));
        versions[0].Id.Should().Be(v1.Value.Id, "el historial nunca se borra ni se reutiliza");
        versions[0].JournalEntryId.Should().Be(v1.Value.JournalEntryId);
        (await SendAsync(new GetOpeningBalanceDateQuery())).Value!.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Version_que_nunca_se_publico_no_fija_la_fecha()
    {
        (await PublishAsync(Debit(await AccountAsync("3.1.02.001"), 1m), Credit(await BridgeAsync(), 1m)))
            .Code.Should().Be(PublishOpeningJournalEntryCommandHandler.BridgeNotClearedCode);

        var date = await SendAsync(new GetOpeningBalanceDateQuery());

        date.Value!.IsLocked.Should().BeFalse("solo una versión que llegó a Posted fija la fecha");
        (await SendAsync(new SetOpeningBalanceDateCommand(Cutoff.AddDays(-1)))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reverso_generico_del_ASI_se_rechaza()
    {
        await LoadBridgeAsync();
        var published = await PublishAsync(await ValidLinesAsync());

        var result = await SendAsync(new ReverseJournalEntryCommand(published.Value!.JournalEntryId!.Value, "corrección"));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ReverseJournalEntryCommandHandler.OpeningReversalNotAllowedCode);
        (await StateAsync())!.Status.Should().Be(OpeningBalancePostingStatus.Posted);
    }

    [Fact]
    public async Task Lineas_con_mas_de_dos_decimales_o_ambos_lados_se_rechazan_en_validacion()
    {
        var bridge = await BridgeAsync();
        var capital = await AccountAsync("3.1.01.001");

        await FluentActions.Awaiting(() => PublishAsync(Debit(bridge, 10.001m), Credit(capital, 10.001m)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*2 decimales*");
        await FluentActions.Awaiting(() => PublishAsync(new OpeningJournalEntryLineInput(bridge, 10m, 10m), Credit(capital, 10m)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*solo en Debe o solo en Haber*");
        await FluentActions.Awaiting(() => PublishAsync(Debit(bridge, 10m)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*entre 2 y*");
        (await StateAsync()).Should().BeNull();
    }
}
