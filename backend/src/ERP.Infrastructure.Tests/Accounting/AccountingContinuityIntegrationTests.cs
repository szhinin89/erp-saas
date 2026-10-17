using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.Continuity;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 (PostgreSQL 16 real vía Testcontainers) — ejecutor único de intentos
/// sobre la obligación contable durable, reproceso manual, aislamiento Tenant/Company y política
/// operativa versionada por Company. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountingContinuityIntegrationTests : IAsyncLifetime
{
    private static readonly DateOnly EntryDate = new(2026, 7, 25);
    private const string ManualReason = "Regla contable corregida";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_accounting_continuity_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherCompanyId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Continuity", $"cont-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Empresa A", createdBy: _userId);
        var other = Company.CreateManaged(tenant.Id, "1790012346001", "Empresa B", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, other);
        db.AccountingOperationalPolicies.Add(
            AccountingContinuityTestWiring.EnabledPolicy(tenant.Id, company.Id, _userId)
        );
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherCompanyId = other.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
                .Options,
            new TestTenant(tenantId),
            new NoPublisher(),
            new TestCompany(companyId)
        );

    private ErpDbContext CreateContext() => CreateContext(_tenantId, _companyId);

    private static PostingEngine Engine(ErpDbContext db) =>
        new(
            new JournalEntryRepository(db),
            new PostingRuleRepository(db),
            new AccountingPeriodRepository(db),
            new JournalEntrySequenceRepository(db),
            new AccountRepository(db),
            NullLogger<PostingEngine>.Instance
        );

    private AccountingPostingExecutor Executor(
        ErpDbContext db,
        Guid companyId,
        Guid? userId = null,
        IPostingEngine? engine = null,
        Guid? tenantId = null,
        IUnitOfWork? unitOfWork = null
    )
    {
        var company = new TestCompany(companyId);
        return new AccountingPostingExecutor(
            unitOfWork ?? new UnitOfWork(db),
            new AccountingPostingRepository(db),
            new AccountingPostingFactBuilder(
                new ExpenseCategoryRepository(db, company),
                new AccountRepository(db),
                new ERP.Infrastructure.Persistence.Repositories.Caja.CashRegisterRepository(db, company),
                new ERP.Infrastructure.Persistence.Repositories.Finance.CompanyBankAccountRepository(db, company),
                new ERP.Infrastructure.Persistence.Repositories.Sales.PaymentMethodAccountRepository(db),
                new PostingRuleRepository(db)
            ),
            engine ?? Engine(db),
            new AccountingPostingDispatchQueue(),
            new TestTenant(tenantId ?? _tenantId),
            company,
            Mock.Of<ICurrentUser>(u => u.UserId == (userId ?? _userId)),
            new TestAuditContext(_tenantId, companyId, userId ?? _userId),
            NullLogger<AccountingPostingExecutor>.Instance,
            new ERP.Application.Modules.Accounting.UseCases.JournalEntries.JournalEntryReversal(
                new JournalEntryRepository(db), new AccountingPeriodRepository(db),
                new JournalEntrySequenceRepository(db), new AccountingOperationalPolicyRepository(db)),
            new JournalEntryRepository(db), new AccountingOperationalPolicyRepository(db),
            StandardPrecisionPolicyProvider.Instance
        );
    }

    private async Task SeedRuleAndPeriodAsync(Guid companyId)
    {
        await using var db = CreateContext(_tenantId, companyId);
        var debit = Account.Create(_tenantId, companyId, AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]),
            "Compras", null, AccountType.Expense, AccountNature.Debit, allowsPosting: true, createdBy: _userId);
        var credit = Account.Create(_tenantId, companyId, AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]),
            "Cuentas por pagar", null, AccountType.Liability, AccountNature.Credit, allowsPosting: true, createdBy: _userId);
        var rule = PostingRule.Create(_tenantId, companyId, "Purchases", "InvoiceReceived", _userId);
        rule.AddLine(debit.Id, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(credit.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);
        var period = AccountingPeriod.Create(_tenantId, companyId, 2026, 7,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), _userId);
        db.Accounts.AddRange(debit, credit);
        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private static AccountingPostingPayload Payload() => new(100m, 0m, 0m, 0m, 100m);

    private async Task<AccountingPosting> SeedPendingAsync(
        Guid companyId,
        Guid? sourceEventId = null,
        DateOnly? entryDate = null
    )
    {
        await using var db = CreateContext(_tenantId, companyId);
        var posting = AccountingPosting.CreatePending(
            _tenantId,
            companyId,
            "Purchases",
            "InvoiceReceived",
            sourceEventId ?? Guid.NewGuid(),
            "001-001-000000001",
            entryDate ?? EntryDate,
            entryDate ?? EntryDate,
            AccountingPostingPayload.CurrentVersion,
            Payload().Serialize(),
            _userId,
            "seed"
        );
        db.AccountingPostings.Add(posting);
        await db.SaveChangesAsync();
        return posting;
    }

    // ── Ejecutor: concurrencia e idempotencia ─────────────────────────────

    [Fact]
    public async Task Dos_ejecutores_concurrentes_publican_un_solo_asiento()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var posting = await SeedPendingAsync(_companyId);
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Result<AccountingPostingDto>> Run() =>
            Task.Run(async () =>
            {
                await go.Task;
                await using var db = CreateContext();
                return await Executor(db, _companyId)
                    .ExecuteAsync(posting.Id, AccountingPostingAttemptTrigger.Manual, ManualReason, CancellationToken.None);
            });

        var a = Run();
        var b = Run();
        go.SetResult();
        var results = await Task.WhenAll(a, b);

        results.Should().OnlyContain(r => r.IsSuccess && r.Value!.Status == AccountingPostingStatus.Posted);
        await using var verify = CreateContext();
        (await verify.JournalEntries.CountAsync(e => e.SourceEventId == posting.SourceEventId)).Should().Be(1);
        var stored = await verify.AccountingPostings.SingleAsync(p => p.Id == posting.Id);
        stored.Status.Should().Be(AccountingPostingStatus.Posted);
        stored.Attempts.Should().Be(1, "el segundo ejecutor encontró la obligación ya resuelta");
        (await verify.AccountingPostingAttempts.CountAsync(x => x.AccountingPostingId == posting.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Obligacion_Pending_con_asiento_ya_existente_queda_Posted_sin_duplicar()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var posting = await SeedPendingAsync(_companyId);
        Guid existingEntryId;
        // Simula un intento previo cuyo asiento se publicó pero cuyo resultado no llegó a la obligación.
        await using (var db = CreateContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var outcome = await Engine(db).PostAsync(
                new PostingFact(_tenantId, _companyId, "Purchases", "InvoiceReceived",
                    posting.SourceEventId, EntryDate, 100m, 0m, 0m, 0m, 100m),
                CancellationToken.None
            );
            outcome.IsSuccess.Should().BeTrue(outcome.Error);
            existingEntryId = outcome.Value!.JournalEntryId;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await using (var db = CreateContext())
        {
            var result = await Executor(db, _companyId)
                .ExecuteAsync(posting.Id, AccountingPostingAttemptTrigger.Manual, ManualReason, CancellationToken.None);
            result.Value!.Status.Should().Be(AccountingPostingStatus.Posted);
            result.Value.JournalEntryId.Should().Be(existingEntryId);
        }

        await using var verify = CreateContext();
        (await verify.JournalEntries.CountAsync(e => e.SourceEventId == posting.SourceEventId)).Should().Be(1);
    }

    [Fact]
    public async Task Excepcion_interna_del_motor_deja_Failed_INTERNAL_con_intento_durable()
    {
        var posting = await SeedPendingAsync(_companyId);
        var throwing = new Mock<IPostingEngine>();
        throwing
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await using (var db = CreateContext())
        {
            var result = await Executor(db, _companyId, engine: throwing.Object)
                .ExecuteAsync(posting.Id, AccountingPostingAttemptTrigger.Initial, null, CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
            result.Value!.Status.Should().Be(AccountingPostingStatus.Failed);
        }

        await using var verify = CreateContext();
        var stored = await verify.AccountingPostings.SingleAsync(p => p.Id == posting.Id);
        stored.Status.Should().Be(AccountingPostingStatus.Failed);
        stored.ErrorCode.Should().Be(AccountingPostingExecutor.InternalErrorCode);
        stored.ErrorMessage.Should().NotContain("boom", "el detalle interno no se expone");
        var attempt = await verify.AccountingPostingAttempts.SingleAsync(x => x.AccountingPostingId == posting.Id);
        attempt.Outcome.Should().Be(AccountingPostingStatus.Failed);
        attempt.ErrorCode.Should().Be(AccountingPostingExecutor.InternalErrorCode);
        (await verify.JournalEntries.AnyAsync(e => e.SourceEventId == posting.SourceEventId)).Should().BeFalse();
    }

    [Fact]
    public async Task Reproceso_manual_tras_corregir_configuracion_publica_y_conserva_historial()
    {
        var posting = await SeedPendingAsync(_companyId);
        await using (var db = CreateContext())
            (await Executor(db, _companyId)
                .ExecuteAsync(posting.Id, AccountingPostingAttemptTrigger.Initial, null, CancellationToken.None))
                .Value!.Status.Should().Be(AccountingPostingStatus.Failed);

        await SeedRuleAndPeriodAsync(_companyId);
        var reprocessor = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            var handler = new ReprocessAccountingPostingHandler(Executor(db, _companyId, reprocessor));
            var result = await handler.Handle(
                new ReprocessAccountingPostingCommand(posting.Id, ManualReason),
                CancellationToken.None
            );
            result.Value!.Status.Should().Be(AccountingPostingStatus.Posted);
        }

        await using var verify = CreateContext();
        var stored = await verify.AccountingPostings.SingleAsync(p => p.Id == posting.Id);
        stored.Attempts.Should().Be(2);
        stored.LastAttemptAtUtc.Should().NotBeNull();
        stored.ErrorCode.Should().BeNull();
        stored.SourceEventId.Should().Be(posting.SourceEventId, "la identidad no cambia por intento");
        var attempts = await verify.AccountingPostingAttempts
            .Where(x => x.AccountingPostingId == posting.Id)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync();
        attempts.Select(x => x.Trigger).Should().Equal(AccountingPostingAttemptTrigger.Initial, AccountingPostingAttemptTrigger.Manual);
        attempts.Select(x => x.Outcome).Should().Equal(AccountingPostingStatus.Failed, AccountingPostingStatus.Posted);
        attempts[0].ErrorCode.Should().Be("RULE_NOT_FOUND");
        attempts[1].TriggeredBy.Should().Be(reprocessor);
        attempts[1].JournalEntryId.Should().Be(stored.JournalEntryId);
        attempts[1].CorrelationId.Should().Be(TestAuditContext.Correlation);
        attempts[0].Reason.Should().BeNull("el intento inicial es del sistema");
        attempts[1].Reason.Should().Be(ManualReason);
    }

    // ── Aislamiento Tenant/Company ─────────────────────────────────────────

    [Fact]
    public async Task Obligacion_de_otra_empresa_no_se_ve_ni_se_reprocesa()
    {
        var posting = await SeedPendingAsync(_companyId);

        await using var db = CreateContext(_tenantId, _otherCompanyId);
        var reprocess = await Executor(db, _otherCompanyId)
            .ExecuteAsync(posting.Id, AccountingPostingAttemptTrigger.Manual, ManualReason, CancellationToken.None);
        reprocess.IsSuccess.Should().BeFalse();
        reprocess.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        var repo = new AccountingPostingRepository(db);
        (await repo.ListAsync(_tenantId, _otherCompanyId, null, null, null, 100)).Should().BeEmpty();
        (await repo.GetByIdAsync(_tenantId, _otherCompanyId, posting.Id)).Should().BeNull();
        (await repo.GetByIdAsync(_tenantId, _companyId, posting.Id))
            .Should().BeNull("el query filter fail-closed de la empresa B oculta la fila aunque se pida la empresa A");

        await using var verify = CreateContext();
        (await verify.AccountingPostings.SingleAsync(p => p.Id == posting.Id))
            .Attempts.Should().Be(0);
    }

    // ── Política operativa por Company ─────────────────────────────────────

    private SetAccountingOperationalModeHandler SetModeHandler(ErpDbContext db, Guid companyId) =>
        new(
            new AccountingOperationalPolicyRepository(db),
            new CompanyRepository(db),
            new AccountingOperationalPolicyReader(
                new AccountingOperationalPolicyRepository(db),
                new CompanyRepository(db),
                new CompanyClock(db)
            ),
            new AccountingModeChangePreconditions(
                new AccountingPostingRepository(db),
                new ERP.Infrastructure.Persistence.Repositories.Inventory.InventoryCostLedger(db),
                new OpeningAccountingCompleteness(
                    new ERP.Infrastructure.InitialLoad.OpeningBalanceConstraintsReader(db, new TestCompany(companyId)),
                    new ERP.Infrastructure.Persistence.Repositories.InitialLoad.ImportBatchRepository(db),
                    new ERP.Infrastructure.Persistence.Repositories.InitialLoad.OpeningBalancePostingRepository(db),
                    new ERP.Infrastructure.Persistence.Repositories.InitialLoad.OpeningJournalEntryPostingRepository(db)
                ),
                new AccountingRelevantFactsReader(db, new ERP.Infrastructure.Persistence.Services.CompanyClock(db))
            ),
            new CompanyClock(db),
            new UnitOfWork(db),
            new TestTenant(_tenantId),
            new TestCompany(companyId),
            Mock.Of<ICurrentUser>(u => u.UserId == _userId)
        );

    private async Task SetRequiredAsync(Guid companyId)
    {
        await using var db = CreateContext(_tenantId, companyId);
        var company = await db.Companies.SingleAsync(c => c.Id == companyId);
        company.UpdateFiscalSettings(null, true, null, false, false, false, _userId);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Empresa_obligada_no_puede_deshabilitar_Contabilidad()
    {
        await SetRequiredAsync(_companyId);

        await using var db = CreateContext();
        var result = await SetModeHandler(db, _companyId).Handle(
            new SetAccountingOperationalModeCommand(AccountingOperationalMode.Disabled, new DateOnly(2027, 1, 1), null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("obligada");
        (await db.AccountingOperationalPolicies.CountAsync(p => p.CompanyId == _companyId)).Should().Be(1);
    }

    /// <summary>
    /// ACCOUNTING-CONTINUITY-05 — Disabled solo procede sin obligaciones sin resolver y sin
    /// retroactividad; resuelta la pendiente, la versión Disabled se registra.
    /// </summary>
    [Fact]
    public async Task C05_Disabled_se_bloquea_por_pendientes_y_retroactividad_y_procede_al_resolverlas()
    {
        var pending = await SeedPendingAsync(_companyId);
        DateOnly today;
        await using (var db = CreateContext())
            today = await new CompanyClock(db).TodayAsync(_companyId, _tenantId, CancellationToken.None);

        await using (var db = CreateContext())
        {
            var retro = await SetModeHandler(db, _companyId).Handle(
                new SetAccountingOperationalModeCommand(AccountingOperationalMode.Disabled, today.AddDays(-1), null),
                CancellationToken.None);
            retro.IsSuccess.Should().BeFalse();
            retro.Error.Should().Contain("retroactiva");

            var blocked = await SetModeHandler(db, _companyId).Handle(
                new SetAccountingOperationalModeCommand(AccountingOperationalMode.Disabled, today, null),
                CancellationToken.None);
            blocked.IsSuccess.Should().BeFalse();
            blocked.Error.Should().Contain("pendientes o fallidas");
        }

        await using (var db = CreateContext())
        {
            (await db.AccountingPostings.SingleAsync(p => p.Id == pending.Id)).Cancel(_userId);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var ok = await SetModeHandler(db, _companyId).Handle(
                new SetAccountingOperationalModeCommand(AccountingOperationalMode.Disabled, today, "Sin uso contable"),
                CancellationToken.None);
            ok.IsSuccess.Should().BeTrue(ok.Error);
            ok.Value!.CurrentMode.Should().Be(AccountingOperationalMode.Disabled);
        }

        await using var verify = CreateContext();
        var decision = new AccountingOperationalDecision(new AccountingOperationalPolicyRepository(verify));
        (await decision.ResolveAsync(_tenantId, _companyId, today, CancellationToken.None))
            .Should().Be(AccountingApplicability.NotApplicable, "hechos desde la vigencia Disabled no generan obligaciones");
        (await decision.ResolveAsync(_tenantId, _companyId, today.AddDays(-1), CancellationToken.None))
            .Should().Be(AccountingApplicability.Applicable, "la vigencia previa no cambia retroactivamente");
    }

    /// <summary>
    /// ACCOUNTING-CONTINUITY-05 — una cobertura de costo cuyo reconocimiento original sigue Failed
    /// queda Failed COST_DEPENDENCY_PENDING; el original no se reintenta implícitamente.
    /// </summary>
    [Fact]
    public async Task C05_cobertura_con_costo_original_Failed_no_lo_reintenta_y_queda_Failed_por_dependencia()
    {
        var invoiceId = Guid.NewGuid();
        AccountingPosting Cost(Guid sourceId, string factType) => AccountingPosting.CreatePending(
            _tenantId, _companyId, "Sales", factType, sourceId, "001-001-000000009", EntryDate, EntryDate,
            AccountingPostingPayload.CurrentVersion,
            new AccountingPostingPayload(0m, 0m, 0m, 0m, 0m, HistoricalCostTotal: 6m).Serialize(),
            _userId, "seed", invoiceId);
        var original = Cost(invoiceId, "CostOfGoodsSold");
        await using (var db = CreateContext())
        {
            db.AccountingPostings.Add(original);
            await db.SaveChangesAsync();
        }
        await using (var db = CreateContext())
        {
            // Sin regla Sales/CostOfGoodsSold: el intento inicial falla y queda durable.
            var first = await Executor(db, _companyId)
                .RunAsync(original.Id, AccountingPostingAttemptTrigger.Initial, null, null, CancellationToken.None);
            first.Posting!.Status.Should().Be(AccountingPostingStatus.Failed);
        }

        var coverage = Cost(Guid.NewGuid(), "CostOfGoodsSold");
        await using (var db = CreateContext())
        {
            db.AccountingPostings.Add(coverage);
            await db.SaveChangesAsync();
        }
        await using (var db = CreateContext())
        {
            var attempt = await Executor(db, _companyId)
                .RunAsync(coverage.Id, AccountingPostingAttemptTrigger.Initial, null, null, CancellationToken.None);
            attempt.Posting!.Status.Should().Be(AccountingPostingStatus.Failed);
            attempt.Posting.ErrorCode.Should().Be(AccountingPostingExecutor.CostDependencyPendingCode);
        }

        await using var verify = CreateContext();
        var storedOriginal = await verify.AccountingPostings.SingleAsync(p => p.Id == original.Id);
        storedOriginal.Status.Should().Be(AccountingPostingStatus.Failed);
        storedOriginal.Attempts.Should().Be(1, "la cobertura no reintenta el reconocimiento original");
        (await verify.JournalEntries.CountAsync(e => e.SourceEventId == invoiceId || e.SourceEventId == coverage.SourceEventId))
            .Should().Be(0);
    }

    // ── ACCOUNTING-CONTINUITY-05 corrección final: delta de costo y Enabled retroactivo ──────────

    private async Task<Guid> SeedCostRulesAndPeriodAsync()
    {
        await using var db = CreateContext();
        var cost = Account.Create(_tenantId, _companyId, AccountCode.Create($"5.9.{Guid.NewGuid():N}"[..8]),
            "Costo de ventas", null, AccountType.Expense, AccountNature.Debit, allowsPosting: true, createdBy: _userId);
        var inventory = Account.Create(_tenantId, _companyId, AccountCode.Create($"1.9.{Guid.NewGuid():N}"[..8]),
            "Inventario", null, AccountType.Asset, AccountNature.Debit, allowsPosting: true, createdBy: _userId);
        var sold = PostingRule.Create(_tenantId, _companyId, "Sales", "CostOfGoodsSold", _userId);
        sold.AddLine(cost.Id, AccountNature.Debit, PostingAmountKind.HistoricalCost);
        sold.AddLine(inventory.Id, AccountNature.Credit, PostingAmountKind.HistoricalCost);
        var reversed = PostingRule.Create(_tenantId, _companyId, "Sales", "CostOfGoodsSoldReversed", _userId);
        reversed.AddLine(inventory.Id, AccountNature.Debit, PostingAmountKind.HistoricalCost);
        reversed.AddLine(cost.Id, AccountNature.Credit, PostingAmountKind.HistoricalCost);
        db.Accounts.AddRange(cost, inventory);
        db.PostingRules.AddRange(sold, reversed);
        db.AccountingPeriods.Add(AccountingPeriod.Create(_tenantId, _companyId, 2026, 7,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), _userId));
        await db.SaveChangesAsync();
        return cost.Id;
    }

    /// <summary>Crea y ejecuta (intento inicial) una obligación de costo de la factura.</summary>
    private async Task<AccountingPostingDto> CostAttemptAsync(Guid invoiceId, Guid sourceId, decimal operationalAmount,
        decimal? target = null)
    {
        var posting = AccountingPosting.CreatePending(_tenantId, _companyId, "Sales",
            operationalAmount < 0m ? "CostOfGoodsSoldReversed" : "CostOfGoodsSold", sourceId, "001-001-000000010",
            EntryDate, EntryDate, AccountingPostingPayload.CurrentVersion,
            new AccountingPostingPayload(0m, 0m, 0m, 0m, 0m, HistoricalCostTotal: Math.Abs(operationalAmount),
                RecognizedCostTarget: target).Serialize(),
            _userId, "seed", invoiceId);
        await using (var db = CreateContext())
        {
            db.AccountingPostings.Add(posting);
            await db.SaveChangesAsync();
        }
        await using var run = CreateContext();
        var attempt = await Executor(run, _companyId)
            .RunAsync(posting.Id, AccountingPostingAttemptTrigger.Initial, null, null, CancellationToken.None);
        return attempt.Posting!;
    }

    private async Task<decimal> CostOfSalesBalanceAsync(Guid costAccount)
    {
        await using var db = CreateContext();
        return (await db.JournalEntries.Include(e => e.Lines).ToListAsync())
            .SelectMany(e => e.Lines).Where(l => l.AccountId == costAccount).Sum(l => l.Debit - l.Credit);
    }

    private async Task<decimal> EntryDebitAsync(Guid entryId)
    {
        await using var db = CreateContext();
        return (await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == entryId)).Lines.Sum(l => l.Debit);
    }

    /// <summary>
    /// A — el costo contabilizado (5) difiere del operativo previo (6): con objetivo 9 la cobertura
    /// contabiliza exactamente la diferencia restante 9 − 5 = 4, no el delta operativo 3.
    /// </summary>
    [Fact]
    public async Task C05_cobertura_contabiliza_objetivo_menos_costo_Posted_cuando_el_historico_difiere()
    {
        var costAccount = await SeedCostRulesAndPeriodAsync();
        var invoiceId = Guid.NewGuid();
        (await CostAttemptAsync(invoiceId, invoiceId, 5m)).Status.Should().Be(AccountingPostingStatus.Posted);

        var coverage = await CostAttemptAsync(invoiceId, Guid.NewGuid(), operationalAmount: 3m, target: 9m);

        coverage.Status.Should().Be(AccountingPostingStatus.Posted, coverage.ErrorMessage);
        (await EntryDebitAsync(coverage.JournalEntryId!.Value)).Should().Be(4m);
        (await CostOfSalesBalanceAsync(costAccount)).Should().Be(9m);
    }

    /// <summary>
    /// B — varias coberturas Posted: cada ajuste usa la suma ya contabilizada (sin duplicar) y un
    /// objetivo ya alcanzado se resuelve sin asiento.
    /// </summary>
    [Fact]
    public async Task C05_coberturas_sucesivas_usan_la_suma_contabilizada_sin_duplicar()
    {
        var costAccount = await SeedCostRulesAndPeriodAsync();
        var invoiceId = Guid.NewGuid();
        await CostAttemptAsync(invoiceId, invoiceId, 6m);

        var first = await CostAttemptAsync(invoiceId, Guid.NewGuid(), 2m, target: 8m);
        var second = await CostAttemptAsync(invoiceId, Guid.NewGuid(), 3m, target: 11m);
        var reached = await CostAttemptAsync(invoiceId, Guid.NewGuid(), 1m, target: 11m);

        (await EntryDebitAsync(first.JournalEntryId!.Value)).Should().Be(2m);
        (await EntryDebitAsync(second.JournalEntryId!.Value)).Should().Be(3m);
        reached.Status.Should().Be(AccountingPostingStatus.Posted);
        reached.JournalEntryId.Should().BeNull("el objetivo ya estaba contabilizado: importe cero sin asiento");
        (await CostOfSalesBalanceAsync(costAccount)).Should().Be(11m);
    }

    /// <summary>
    /// Return A — el costo contabilizado (5) difiere del operativo previo (6): una devolución que deja el
    /// costo objetivo en 4 contabiliza exactamente 5 − 4 = 1 (no el delta operativo 2).
    /// </summary>
    [Fact]
    public async Task C05_devolucion_contabiliza_hasta_el_nuevo_objetivo_cuando_el_historico_difiere()
    {
        var costAccount = await SeedCostRulesAndPeriodAsync();
        var invoiceId = Guid.NewGuid();
        await CostAttemptAsync(invoiceId, invoiceId, 5m);

        var returned = await CostAttemptAsync(invoiceId, Guid.NewGuid(), operationalAmount: -2m, target: 4m);

        returned.Status.Should().Be(AccountingPostingStatus.Posted, returned.ErrorMessage);
        returned.FactType.Should().Be("CostOfGoodsSoldReversed");
        (await EntryDebitAsync(returned.JournalEntryId!.Value)).Should().Be(1m);
        (await CostOfSalesBalanceAsync(costAccount)).Should().Be(4m);
    }

    /// <summary>
    /// Return B — tras varias coberturas Posted (6 → 8 → 11), la devolución al objetivo 7 usa la suma
    /// realmente contabilizada: reversa 4, ni duplica ni sobre-reversa el delta operativo 5.
    /// </summary>
    [Fact]
    public async Task C05_devolucion_tras_coberturas_usa_la_suma_contabilizada_sin_sobre_reversar()
    {
        var costAccount = await SeedCostRulesAndPeriodAsync();
        var invoiceId = Guid.NewGuid();
        await CostAttemptAsync(invoiceId, invoiceId, 6m);
        await CostAttemptAsync(invoiceId, Guid.NewGuid(), 2m, target: 8m);
        await CostAttemptAsync(invoiceId, Guid.NewGuid(), 3m, target: 11m);

        var returned = await CostAttemptAsync(invoiceId, Guid.NewGuid(), operationalAmount: -5m, target: 7m);

        returned.Status.Should().Be(AccountingPostingStatus.Posted, returned.ErrorMessage);
        (await EntryDebitAsync(returned.JournalEntryId!.Value)).Should().Be(4m);
        (await CostOfSalesBalanceAsync(costAccount)).Should().Be(7m);
    }

    /// <summary>
    /// D — primera elección de modo con vigencia Enabled pasada: rechazada si ya existe una operación
    /// contablemente relevante desde esa fecha; no se crean versiones ni obligaciones históricas.
    /// </summary>
    [Fact]
    public async Task C05_primera_eleccion_Enabled_retroactiva_con_operacion_existente_se_rechaza()
    {
        var effectiveFrom = new DateOnly(2026, 8, 1);
        await using (var db = CreateContext(_tenantId, _otherCompanyId))
        {
            var partner = ERP.Domain.MasterData.Entities.BusinessPartner.Create(
                _tenantId, "04", "1790016919001", null, "Cliente C05", _userId);
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            var payment = ERP.Domain.Modules.Finance.Entities.Payment.Create(_tenantId, _otherCompanyId,
                ERP.Domain.Modules.Finance.Enums.PaymentDirection.Collection, partner.Id, 10m,
                effectiveFrom.AddDays(5), null, "C05", _userId);
            db.Payments.Add(payment);
            db.Entry(payment).Property(x => x.Status).CurrentValue = ERP.Domain.Modules.Finance.Enums.PaymentStatus.Applied;
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext(_tenantId, _otherCompanyId))
        {
            var result = await SetModeHandler(db, _otherCompanyId).Handle(
                new SetAccountingOperationalModeCommand(AccountingOperationalMode.Enabled, effectiveFrom, null),
                CancellationToken.None);
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("retroactivamente");
        }

        await using var verify = CreateContext(_tenantId, _otherCompanyId);
        (await verify.AccountingOperationalPolicies.AnyAsync(p => p.CompanyId == _otherCompanyId)).Should().BeFalse();
        (await verify.AccountingPostings.AnyAsync(p => p.CompanyId == _otherCompanyId)).Should().BeFalse();
    }

    [Fact]
    public async Task Empresa_nueva_elige_Enabled_explicitamente_con_vigencia_y_sin_historia()
    {
        await using (var db = CreateContext(_tenantId, _otherCompanyId))
        {
            var before = await new AccountingOperationalPolicyReader(
                    new AccountingOperationalPolicyRepository(db),
                    new CompanyRepository(db),
                    new CompanyClock(db))
                .ReadAsync(_tenantId, _otherCompanyId, CancellationToken.None);
            before!.IsConfigured.Should().BeFalse();

            var result = await SetModeHandler(db, _otherCompanyId).Handle(
                new SetAccountingOperationalModeCommand(AccountingOperationalMode.Enabled, new DateOnly(2026, 8, 1), "Inicio de contabilidad"),
                CancellationToken.None
            );
            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value!.IsConfigured.Should().BeTrue();
            result.Value.Versions.Should().ContainSingle()
                .Which.Should().Match<AccountingOperationalPolicyVersionDto>(v =>
                    v.Mode == AccountingOperationalMode.Enabled
                    && v.EffectiveFrom == new DateOnly(2026, 8, 1)
                    && v.EffectiveTo == null);

            var again = await SetModeHandler(db, _otherCompanyId).Handle(
                new SetAccountingOperationalModeCommand(AccountingOperationalMode.Enabled, new DateOnly(2026, 9, 1), null),
                CancellationToken.None
            );
            again.IsSuccess.Should().BeFalse("ya está Enabled");
        }

        await using var verify = CreateContext(_tenantId, _otherCompanyId);
        (await verify.AccountingPostings.AnyAsync()).Should().BeFalse("activar no genera obligaciones históricas");
        var decision = new AccountingOperationalDecision(new AccountingOperationalPolicyRepository(verify));
        (await decision.ResolveAsync(_tenantId, _otherCompanyId, new DateOnly(2026, 7, 31), CancellationToken.None))
            .Should().Be(AccountingApplicability.NotApplicable);
        (await decision.ResolveAsync(_tenantId, _otherCompanyId, new DateOnly(2026, 8, 1), CancellationToken.None))
            .Should().Be(AccountingApplicability.Applicable);
    }

    private UpdateCompanyFiscalHandler FiscalHandler(ErpDbContext db, Guid companyId)
    {
        var guard = new Mock<ICompanyAccessGuard>();
        guard
            .Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(
                new CompanyAccessContext(_userId, _tenantId, companyId, "Admin", true, true)));
        return new UpdateCompanyFiscalHandler(
            guard.Object,
            new CompanyRepository(db),
            Mock.Of<ICurrentUser>(u => u.UserId == _userId),
            new AccountingOperationalPolicyRepository(db),
            new CompanyClock(db),
            new UnitOfWork(db)
        );
    }

    [Fact]
    public async Task Cambio_fiscal_a_obligada_estando_Disabled_se_rechaza()
    {
        // Versión Disabled vigente (escrita directo: el comando aún no permite Disabled).
        await using (var db = CreateContext())
        {
            var enabled = await db.AccountingOperationalPolicies.SingleAsync(p => p.CompanyId == _companyId);
            enabled.EndBefore(new DateOnly(2020, 1, 1), _userId);
            db.AccountingOperationalPolicies.Add(AccountingOperationalPolicy.Create(
                _tenantId, _companyId, AccountingOperationalMode.Disabled, new DateOnly(2020, 1, 1), "test", _userId));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var result = await FiscalHandler(db, _companyId).Handle(
                new UpdateCompanyFiscalCommand(null, true, null, false, false, false),
                CancellationToken.None
            );
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("primero habilite Contabilidad");
        }

        await using var verify = CreateContext();
        (await verify.Companies.SingleAsync(c => c.Id == _companyId)).IsAccountingReq.Should().BeFalse();
    }

    [Fact]
    public async Task Cambio_fiscal_a_obligada_sin_modo_definido_se_rechaza_y_con_Enabled_se_acepta()
    {
        await using (var db = CreateContext(_tenantId, _otherCompanyId))
        {
            var rejected = await FiscalHandler(db, _otherCompanyId).Handle(
                new UpdateCompanyFiscalCommand(null, true, null, false, false, false),
                CancellationToken.None
            );
            rejected.IsSuccess.Should().BeFalse("no se activa Contabilidad automáticamente");
        }

        await using (var db = CreateContext())
        {
            var accepted = await FiscalHandler(db, _companyId).Handle(
                new UpdateCompanyFiscalCommand(null, true, null, false, false, false),
                CancellationToken.None
            );
            accepted.IsSuccess.Should().BeTrue(accepted.Error);
        }

        await using var verify = CreateContext();
        (await verify.Companies.SingleAsync(c => c.Id == _companyId)).IsAccountingReq.Should().BeTrue();
        (await verify.Companies.SingleAsync(c => c.Id == _otherCompanyId)).IsAccountingReq.Should().BeFalse();
    }

    // ── Dobles ─────────────────────────────────────────────────────────────


    private ReprocessAccountingPostingsHandler Batch(ErpDbContext db, Guid? companyId = null, Guid? tenantId = null) =>
        new(Executor(db, companyId ?? _companyId, tenantId: tenantId),
            new TestAuditContext(tenantId ?? _tenantId, companyId ?? _companyId, _userId));

    [Fact]
    public async Task Batch_mixed_deduplicates_and_records_actor_reason_correlation()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var good = await SeedPendingAsync(_companyId);
        var failed = await SeedPendingAsync(_companyId, entryDate: new DateOnly(2027, 1, 1));
        var canceled = await SeedPendingAsync(_companyId);
        var foreign = await SeedPendingAsync(_otherCompanyId);
        await using (var seed = CreateContext())
        {
            (await seed.AccountingPostings.SingleAsync(x => x.Id == canceled.Id)).Cancel(_userId);
            await seed.SaveChangesAsync();
        }
        await using var db = CreateContext();
        (await Executor(db, _companyId).ExecuteAsync(failed.Id, AccountingPostingAttemptTrigger.Initial, null, CancellationToken.None))
            .Value!.Status.Should().Be(AccountingPostingStatus.Failed);
        var ids = new[] { failed.Id, good.Id, canceled.Id, foreign.Id, Guid.NewGuid(), good.Id };
        var result = (await Batch(db).Handle(new(ids, "  Manual correction  "), CancellationToken.None)).Value!;
        result.Items.Select(x => x.Outcome).Should().Equal(
            AccountingPostingBatchItemOutcome.Attempted, AccountingPostingBatchItemOutcome.Attempted,
            AccountingPostingBatchItemOutcome.AlreadyResolved, AccountingPostingBatchItemOutcome.NotFound,
            AccountingPostingBatchItemOutcome.NotFound);
        result.Items[0].Posting!.Status.Should().Be(AccountingPostingStatus.Failed);
        result.Items[1].Posting!.Status.Should().Be(AccountingPostingStatus.Posted);
        var again = (await Batch(db).Handle(new(new[] { good.Id }, ManualReason), CancellationToken.None)).Value!;
        again.Items.Single().Outcome.Should().Be(AccountingPostingBatchItemOutcome.AlreadyResolved);
        await using var verify = CreateContext();
        var attempts = await verify.AccountingPostingAttempts.Where(x => x.Trigger == AccountingPostingAttemptTrigger.Manual).ToListAsync();
        attempts.Should().HaveCount(2).And.OnlyContain(x => x.Reason == "Manual correction"
            && x.TriggeredBy == _userId && x.CorrelationId == result.CorrelationId);
        var otherTenant = Guid.NewGuid();
        await using var wrongTenant = CreateContext(otherTenant, _companyId);
        var hidden = (await Batch(wrongTenant, tenantId: otherTenant).Handle(new(new[] { good.Id }, ManualReason), CancellationToken.None)).Value!;
        hidden.Items.Single().Outcome.Should().Be(AccountingPostingBatchItemOutcome.NotFound);
    }

    [Fact]
    public async Task Batch_concurrent_same_ids_posts_once()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var a = await SeedPendingAsync(_companyId);
        var b = await SeedPendingAsync(_companyId);
        async Task<AccountingPostingBatchReprocessDto> Run()
        {
            await using var db = CreateContext();
            return (await Batch(db).Handle(new(new[] { a.Id, b.Id }, ManualReason), CancellationToken.None)).Value!;
        }
        var results = await Task.WhenAll(Run(), Run());
        results.SelectMany(x => x.Items).Count(x => x.Outcome == AccountingPostingBatchItemOutcome.Attempted).Should().Be(2);
        results.SelectMany(x => x.Items).Count(x => x.Outcome == AccountingPostingBatchItemOutcome.AlreadyResolved).Should().Be(2);
        await using var verify = CreateContext();
        (await verify.JournalEntries.CountAsync()).Should().Be(2);
        (await verify.AccountingPostingAttempts.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Batch_sequence_is_fresh_between_attempts_but_keeps_multiple_reservations_in_transaction()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var a = await SeedPendingAsync(_companyId);
        var b = await SeedPendingAsync(_companyId);
        var c = await SeedPendingAsync(_companyId);
        await using var db = CreateContext();
        var executor = Executor(db, _companyId);
        (await executor.ExecuteAsync(a.Id, AccountingPostingAttemptTrigger.Manual, ManualReason, CancellationToken.None)).Value!.Status.Should().Be(AccountingPostingStatus.Posted);
        await using (var other = CreateContext())
            (await Executor(other, _companyId).ExecuteAsync(b.Id, AccountingPostingAttemptTrigger.Manual, ManualReason, CancellationToken.None)).Value!.Status.Should().Be(AccountingPostingStatus.Posted);
        (await executor.ExecuteAsync(c.Id, AccountingPostingAttemptTrigger.Manual, ManualReason, CancellationToken.None)).Value!.Status.Should().Be(AccountingPostingStatus.Posted);
        (await db.JournalEntries.OrderBy(x => x.EntryNumber).Select(x => x.EntryNumber).ToListAsync()).Should().Equal(1, 2, 3);
        await using var tx = await db.Database.BeginTransactionAsync();
        var sequences = new JournalEntrySequenceRepository(db);
        (await sequences.ReserveNextNumberAsync(_tenantId, _companyId, 2026)).Should().Be(4);
        (await sequences.ReserveNextNumberAsync(_tenantId, _companyId, 2026)).Should().Be(5);
        (await sequences.ReserveNextNumberAsync(_tenantId, _companyId, 2028)).Should().Be(1);
        (await sequences.ReserveNextNumberAsync(_tenantId, _companyId, 2028)).Should().Be(2);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    [Fact]
    public async Task Batch_canceled_before_start_marks_all_not_attempted()
    {
        var first = await SeedPendingAsync(_companyId);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var db = CreateContext();
        var result = (await Batch(db).Handle(new(new[] { first.Id, Guid.NewGuid() }, ManualReason), cancellation.Token)).Value!;
        result.Items.Should().OnlyContain(x => x.Outcome == AccountingPostingBatchItemOutcome.NotAttempted);
        (await db.AccountingPostingAttempts.CountAsync()).Should().Be(0);
    }


    [Fact]
    public async Task Batch_cancellation_after_commit_keeps_first_and_skips_rest()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var first = await SeedPendingAsync(_companyId);
        var second = await SeedPendingAsync(_companyId);
        using var cancellation = new CancellationTokenSource();
        await using var db = CreateContext();
        var unit = new UnitOfWork(db);
        var wrapper = new Mock<IUnitOfWork>();
        wrapper.SetupGet(x => x.HasActiveTransaction).Returns(() => unit.HasActiveTransaction);
        wrapper.Setup(x => x.ClearChangeTracker()).Callback(unit.ClearChangeTracker);
        wrapper.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => unit.BeginTransactionAsync(ct));
        wrapper.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => unit.SaveChangesAsync(ct));
        wrapper.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => unit.RollbackAsync(ct));
        wrapper.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) => { await unit.CommitAsync(ct); cancellation.Cancel(); });
        var handler = new ReprocessAccountingPostingsHandler(
            Executor(db, _companyId, unitOfWork: wrapper.Object), new TestAuditContext(_tenantId, _companyId, _userId));
        var result = (await handler.Handle(new(new[] { first.Id, second.Id }, ManualReason), cancellation.Token)).Value!;
        result.Items.Select(x => x.Outcome).Should().Equal(AccountingPostingBatchItemOutcome.Attempted, AccountingPostingBatchItemOutcome.NotAttempted);
        await using var verify = CreateContext();
        (await verify.AccountingPostings.SingleAsync(x => x.Id == first.Id)).Status.Should().Be(AccountingPostingStatus.Posted);
        (await verify.AccountingPostings.SingleAsync(x => x.Id == second.Id)).Attempts.Should().Be(0);
    }


    [Fact]
    public async Task Batch_internal_failure_does_not_stop_next_obligation()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var first = await SeedPendingAsync(_companyId);
        var second = await SeedPendingAsync(_companyId);
        await using var db = CreateContext();
        var engine = new Mock<IPostingEngine>();
        engine.Setup(x => x.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Returns((PostingFact fact, CancellationToken ct) => fact.SourceEventId == first.SourceEventId
                ? throw new InvalidOperationException("internal detail") : Engine(db).PostAsync(fact, ct));
        var handler = new ReprocessAccountingPostingsHandler(Executor(db, _companyId, engine: engine.Object),
            new TestAuditContext(_tenantId, _companyId, _userId));
        var result = (await handler.Handle(new(new[] { first.Id, second.Id }, ManualReason), CancellationToken.None)).Value!;
        result.Items.Should().OnlyContain(x => x.Outcome == AccountingPostingBatchItemOutcome.Attempted);
        result.Items[0].Posting!.ErrorCode.Should().Be(AccountingPostingExecutor.InternalErrorCode);
        result.Items[1].Posting!.Status.Should().Be(AccountingPostingStatus.Posted);
        await using var verify = CreateContext();
        (await verify.AccountingPostingAttempts.CountAsync()).Should().Be(2);
        (await verify.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Continuity03_cancellation_serializes_with_original_attempt_without_mirror()
    {
        await SeedRuleAndPeriodAsync(_companyId);
        var pending = await SeedPendingAsync(_companyId);
        await using var cancelDb = CreateContext();
        await using var attemptDb = CreateContext();
        var unit = new UnitOfWork(cancelDb);
        await unit.BeginTransactionAsync();
        var cancellation = new AccountingPostingCancellation(new AccountingPostingRepository(cancelDb),
            new JournalEntryRepository(cancelDb), Mock.Of<ICurrentUser>(x => x.UserId == _userId),
            new AccountingOperationalPolicyRepository(cancelDb));
        var original = await cancellation.PrepareAsync(_tenantId, _companyId, pending.SourceModule,
            pending.FactType, pending.SourceEventId, default);
        original.Should().BeNull();
        var attempt = Executor(attemptDb, _companyId).RunAsync(pending.Id, AccountingPostingAttemptTrigger.Manual,
            "manual concurrent", null, default);
        await using (var probe = CreateContext())
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (await probe.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'transactionid' AND NOT granted").SingleAsync(timeout.Token) == 0)
                await Task.Delay(20, timeout.Token);
        }
        attempt.IsCompleted.Should().BeFalse();
        await cancelDb.SaveChangesAsync();
        await unit.CommitAsync();
        (await attempt.WaitAsync(TimeSpan.FromSeconds(10))).Outcome.Should().Be(AccountingPostingExecutionOutcome.AlreadyResolved);
        await using var verify = CreateContext();
        (await verify.AccountingPostings.SingleAsync(x => x.Id == pending.Id)).Status.Should().Be(AccountingPostingStatus.Canceled);
        (await verify.JournalEntries.CountAsync()).Should().Be(0);
        (await verify.AccountingPostingAttempts.CountAsync()).Should().Be(0);
    }

    private sealed class TestTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class TestCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class TestAuditContext(Guid tenantId, Guid companyId, Guid userId) : IAuditContext
    {
        public const string Correlation = "continuity-correlation";
        public Guid TenantId => tenantId;
        public Guid CompanyId => companyId;
        public AuditActor Actor =>
            new(tenantId, userId, "tester", null, null, null, Correlation, null, AuditSource.UserAction);
    }

    private sealed class NoPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
