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
                new AccountRepository(db)
            ),
            engine ?? Engine(db),
            new AccountingPostingDispatchQueue(),
            new TestTenant(tenantId ?? _tenantId),
            company,
            Mock.Of<ICurrentUser>(u => u.UserId == (userId ?? _userId)),
            new TestAuditContext(_tenantId, companyId, userId ?? _userId),
            NullLogger<AccountingPostingExecutor>.Instance
        );
    }

    private async Task SeedRuleAndPeriodAsync(Guid companyId)
    {
        await using var db = CreateContext(_tenantId, companyId);
        var debit = Account.Create(_tenantId, companyId, AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]),
            "Compras", null, AccountType.Expense, AccountNature.Debit, allowsPosting: true, createdBy: _userId);
        var credit = Account.Create(_tenantId, companyId, AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]),
            "Cuentas por pagar", null, AccountType.Liability, AccountNature.Credit, allowsPosting: true, createdBy: _userId);
        var rule = PostingRule.Create(_tenantId, companyId, "Purchases", "InvoiceReceived", null, null, null, _userId);
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

    [Fact]
    public async Task Disabled_no_esta_disponible_mientras_existan_integraciones_sin_converger()
    {
        await using var db = CreateContext();
        var result = await SetModeHandler(db, _companyId).Handle(
            new SetAccountingOperationalModeCommand(AccountingOperationalMode.Disabled, new DateOnly(2027, 1, 1), null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("aún no está disponible");
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
            new CompanyClock(db)
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
