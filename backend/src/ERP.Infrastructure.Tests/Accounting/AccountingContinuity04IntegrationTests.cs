using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Audit;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Payables.Events;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-04 (PostgreSQL 16 real) — los hechos migrados registran la obligación
/// durable en la transacción operativa, el intento posterior al commit falla de forma durable ante
/// configuración contable inválida, el reproceso manual publica con la configuración vigente y los
/// reversos espejan el asiento histórico exacto. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountingContinuity04IntegrationTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 10, 16);
    private const string Reason = "Cuenta de caja configurada";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_accounting_continuity_04_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _disabledCompanyId;
    private Guid _cash;
    private Guid _bank;
    private Guid _payables;
    private Guid _advances;
    private Guid _cashRegisterId;
    private Guid _bankAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using (var db = Context(Guid.Empty, Guid.Empty))
        {
            await db.Database.MigrateAsync();
            var tenant = Tenant.Create("Continuity 04", $"c04-{Guid.NewGuid():N}"[..16], _userId);
            var company = Company.CreateManaged(tenant.Id, "1790012345001", "Empresa A", createdBy: _userId);
            var disabled = Company.CreateManaged(tenant.Id, "1790012346001", "Empresa B", createdBy: _userId);
            db.Tenants.Add(tenant);
            db.Companies.AddRange(company, disabled);
            db.AccountingOperationalPolicies.AddRange(
                AccountingContinuityTestWiring.EnabledPolicy(tenant.Id, company.Id, _userId),
                AccountingOperationalPolicy.Create(tenant.Id, disabled.Id, AccountingOperationalMode.Disabled,
                    new DateOnly(1900, 1, 1), "Test", _userId)
            );
            await db.SaveChangesAsync();
            (_tenantId, _companyId, _disabledCompanyId) = (tenant.Id, company.Id, disabled.Id);
        }

        await using (var db = Context(_tenantId, _companyId))
        {
            Account New(string code, string name, AccountType type, AccountNature nature) =>
                Account.Create(_tenantId, _companyId, AccountCode.Create(code), name, null, type, nature, allowsPosting: true, createdBy: _userId);
            var cash = New("1.1.01.001", "Caja", AccountType.Asset, AccountNature.Debit);
            var bank = New("1.1.02.001", "Banco", AccountType.Asset, AccountNature.Debit);
            var advances = New("1.1.03.004", "Anticipos a proveedores", AccountType.Asset, AccountNature.Debit);
            var payables = New("2.1.01.001", "Cuentas por pagar", AccountType.Liability, AccountNature.Credit);
            db.Accounts.AddRange(cash, bank, advances, payables);
            (_cash, _bank, _advances, _payables) = (cash.Id, bank.Id, advances.Id, payables.Id);

            var payment = PostingRule.Create(_tenantId, _companyId, "Payables", "SupplierPaymentConfirmed", null, null, null, _userId);
            payment.AddLine(payables.Id, AccountNature.Debit, PostingAmountKind.AppliedToPayable);
            payment.AddLine(advances.Id, AccountNature.Debit, PostingAmountKind.SupplierCredit);
            var applied = PostingRule.Create(_tenantId, _companyId, "Purchases", "SupplierCreditApplied", null, null, null, _userId);
            applied.AddLine(payables.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
            applied.AddLine(advances.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);
            var refunded = PostingRule.Create(_tenantId, _companyId, "Purchases", "SupplierCreditRefunded", null, null, null, _userId);
            refunded.AddLine(advances.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);
            db.PostingRules.AddRange(payment, applied, refunded);
            db.AccountingPeriods.Add(AccountingPeriod.Create(_tenantId, _companyId, 2026, 10,
                new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), _userId));

            var branch = Branch.Create(_tenantId, "Matriz", "Dirección", "001",
                description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
                email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
                managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
                latitude: null, longitude: null, openingDate: null, internalNotes: null,
                isMainBranch: true, createdBy: _userId, companyId: _companyId);
            var register = CashRegister.Create(_tenantId, _companyId, branch.Id, "C1", "Caja 1", _userId);
            var bankEntity = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", _userId);
            db.Branches.Add(branch);
            db.CashRegisters.Add(register);
            db.Banks.Add(bankEntity);
            await db.SaveChangesAsync();
            var bankAccount = CompanyBankAccount.Create(_tenantId, _companyId, bankEntity.Id, BankAccountType.Checking,
                "2200123456", "Pichincha CTE", bank.Id, _userId);
            db.CompanyBankAccounts.Add(bankAccount);
            await db.SaveChangesAsync();
            (_cashRegisterId, _bankAccountId) = (register.Id, bankAccount.Id);
        }
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    // ── Alta: pago proveedor con caja, parcial y anticipo ───────────────────

    [Fact]
    public async Task Pago_con_caja_sin_cuenta_queda_Failed_y_el_reproceso_manual_publica_el_hecho_completo_sin_duplicar()
    {
        var e = new SupplierPaymentConfirmedEvent(_tenantId, Guid.NewGuid(), _companyId, Guid.NewGuid(), 100m, Today,
            [new(null, _cashRegisterId, 60m), new(_bankAccountId, null, 40m)], appliedAmount: 70m);

        await RecordAsync(_companyId, sp => Translator<SupplierPaymentConfirmedPostingTranslator>(sp).Handle(e, default));

        var failed = await PostingAsync("Payables", "SupplierPaymentConfirmed", e.SupplierPaymentId);
        failed.Status.Should().Be(AccountingPostingStatus.Failed, "la caja aún no tiene cuenta contable");
        failed.ErrorCode.Should().Be(AccountingPostingFactBuilder.DestinationAccountNotConfiguredCode);
        (await EntriesAsync(e.SupplierPaymentId)).Should().BeEmpty();

        await using (var db = Context(_tenantId, _companyId))
        {
            var register = await db.CashRegisters.SingleAsync(r => r.Id == _cashRegisterId);
            register.SetAccountingAccount(_cash, _userId);
            await db.SaveChangesAsync();
        }
        await using (var scope = Scope(_companyId))
        {
            var result = await scope.ServiceProvider.GetRequiredService<AccountingPostingExecutor>()
                .ExecuteAsync(failed.Id, AccountingPostingAttemptTrigger.Manual, Reason, default);
            result.Value!.Status.Should().Be(AccountingPostingStatus.Posted, result.Value.ErrorMessage);
        }

        // Re-registrar el mismo hecho (p. ej. evento repetido) no crea otra obligación ni otro asiento.
        await RecordAsync(_companyId, sp => Translator<SupplierPaymentConfirmedPostingTranslator>(sp).Handle(e, default));

        var entry = (await EntriesAsync(e.SupplierPaymentId)).Should().ContainSingle().Subject;
        entry.Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).Should().BeEquivalentTo(new[]
        {
            (_payables, 70m, 0m),
            (_advances, 30m, 0m),
            (_cash, 0m, 60m),
            (_bank, 0m, 40m),
        });
        await using var verify = Context(_tenantId, _companyId);
        (await verify.AccountingPostings.CountAsync(p => p.SourceEventId == e.SupplierPaymentId)).Should().Be(1);
    }

    [Fact]
    public async Task Accounting_Disabled_no_registra_obligacion_y_otra_empresa_no_reprocesa_la_ajena()
    {
        var disabledEvent = new SupplierCreditAppliedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId, _disabledCompanyId, 10m, 0m, _userId);
        await RecordAsync(_disabledCompanyId, sp => Translator<SupplierCreditAppliedPostingTranslator>(sp).Handle(disabledEvent, default));

        var ownEvent = new SupplierCreditAppliedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId, _companyId, 10m, 0m, _userId);
        await RecordAsync(_companyId, sp => Translator<SupplierCreditAppliedPostingTranslator>(sp).Handle(ownEvent, default));
        var own = await PostingAsync("Purchases", "SupplierCreditApplied", ownEvent.SupplierCreditMovementId);
        own.Status.Should().Be(AccountingPostingStatus.Posted);

        await using (var db = Context(Guid.Empty, Guid.Empty))
            (await db.AccountingPostings.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == _disabledCompanyId)).Should().BeFalse();
        await using var scope = Scope(_disabledCompanyId);
        var foreign = await scope.ServiceProvider.GetRequiredService<AccountingPostingExecutor>()
            .ExecuteAsync(own.Id, AccountingPostingAttemptTrigger.Manual, Reason, default);
        foreign.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Registros_concurrentes_del_mismo_hecho_crean_una_sola_obligacion()
    {
        var e = new SupplierCreditAppliedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId, _companyId, 15m, 0m, _userId);
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Run() => Task.Run(async () =>
        {
            await go.Task;
            await RecordAsync(_companyId, sp => Translator<SupplierCreditAppliedPostingTranslator>(sp).Handle(e, default));
        });

        var runs = new[] { Run(), Run() };
        go.SetResult();
        await Task.WhenAll(runs);

        await using var verify = Context(_tenantId, _companyId);
        (await verify.AccountingPostings.CountAsync(p => p.SourceEventId == e.SupplierCreditMovementId)).Should().Be(1);
        (await EntriesAsync(e.SupplierCreditMovementId)).Should().ContainSingle();
    }

    // ── Reversas: espejo histórico exacto ────────────────────────────────────

    [Fact]
    public async Task Reversas_de_aplicacion_y_reembolso_espejan_el_asiento_historico_aunque_cambie_la_regla()
    {
        var applied = new SupplierCreditAppliedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId, _companyId, 25m, 0m, _userId);
        await RecordAsync(_companyId, sp => Translator<SupplierCreditAppliedPostingTranslator>(sp).Handle(applied, default));
        var refundMovement = Guid.NewGuid();
        await RecordAsync(_companyId, sp => sp.GetRequiredService<AccountingPostingRecorder>().RecordAsync(
            new AccountingPostingRequest(_tenantId, _companyId, "Purchases", "SupplierCreditRefunded", refundMovement, null, Today, Today,
                new AccountingPostingPayload(0m, 0m, 0m, 0m, 30m,
                    Destinations: [new(AccountingPostingDestination.BankAccount, _bankAccountId, 30m, AccountNature.Debit)])), default));
        var originals = new[]
        {
            (await EntriesAsync(applied.SupplierCreditMovementId)).Single(),
            (await EntriesAsync(refundMovement)).Single(),
        };

        // Cambio posterior de reglas y cuentas: el reverso no debe leerlos.
        await using (var db = Context(_tenantId, _companyId))
        {
            foreach (var rule in await db.PostingRules.Where(r => r.FactType == "SupplierCreditApplied" || r.FactType == "SupplierCreditRefunded").ToListAsync())
                rule.Disable(_userId);
            var bankAccount = await db.CompanyBankAccounts.SingleAsync(b => b.Id == _bankAccountId);
            db.Entry(bankAccount).Property(b => b.AccountingAccountId).CurrentValue = _cash;
            await db.SaveChangesAsync();
        }

        await RecordAsync(_companyId, sp => Translator<SupplierCreditApplicationReversedPostingTranslator>(sp).Handle(
            new SupplierCreditApplicationReversedEvent(applied.SupplierCreditId, Guid.NewGuid(), applied.SupplierCreditMovementId,
                applied.TargetPurchasePayableId, _tenantId, _companyId, 25m, 25m, _userId), default));
        await RecordAsync(_companyId, sp => Translator<SupplierCreditRefundReversedPostingTranslator>(sp).Handle(
            new SupplierCreditRefundReversedEvent(Guid.NewGuid(), Guid.NewGuid(), refundMovement, _tenantId, _companyId, 30m, 30m, _userId), default));

        await using var verify = Context(_tenantId, _companyId);
        foreach (var original in originals)
        {
            var mirror = await verify.JournalEntries.Include(j => j.Lines).SingleAsync(j => j.OriginalJournalEntryId == original.Id);
            mirror.EntryDate.Should().Be(Today);
            mirror.Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).Should().BeEquivalentTo(
                original.Lines.Select(l => (l.AccountId, l.Credit, l.Debit)), "espejo exacto del asiento publicado");
        }
        originals[0].Lines.Should().Contain(l => l.AccountId == _payables && l.Debit == 25m);
        originals[1].Lines.Should().Contain(l => l.AccountId == _bank && l.Debit == 30m);
    }

    [Fact]
    public async Task Devolucion_de_compra_Failed_sin_asiento_al_anularse_cancela_la_obligacion_sin_espejo()
    {
        var returnId = Guid.NewGuid();
        // Sin PostingRule Purchases/PurchaseReturn: el intento queda Failed, la operación ya está comiteada.
        await RecordAsync(_companyId, sp => sp.GetRequiredService<AccountingPostingRecorder>().RecordAsync(
            PurchaseReturnPosting.Request(_tenantId, _companyId, returnId, "DC-1", Today, 12m, 0m, 0m, 80m, 32m, -5m, 105m,
                Guid.NewGuid(), null), default));
        (await PostingAsync("Purchases", "PurchaseReturn", returnId)).Status.Should().Be(AccountingPostingStatus.Failed);

        await RecordAsync(_companyId, sp => Translator<PurchaseReturnCancelledPostingTranslator>(sp).Handle(
            new PurchaseReturnCancelledEvent(returnId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId, _companyId,
                "DC-1", "anulada", _userId, 80m, 32m, 105m, -5m, 12m, 0m), default));

        (await PostingAsync("Purchases", "PurchaseReturn", returnId)).Status.Should().Be(AccountingPostingStatus.Canceled);
        await using var verify = Context(_tenantId, _companyId);
        (await verify.JournalEntries.AnyAsync(j => j.SourceEventId == returnId)).Should().BeFalse();
        (await verify.AccountingPostings.AnyAsync(p => p.FactType == "Reversal")).Should().BeFalse();
    }

    // ── Infraestructura del test ─────────────────────────────────────────────

    private ErpDbContext Context(Guid tenantId, Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
                .Options,
            new TestTenant(tenantId),
            new NoPublisher(),
            new TestCompany(companyId)
        );

    private AsyncServiceScope Scope(Guid companyId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Context(_tenantId, companyId));
        services.AddSingleton<ICurrentTenant>(new TestTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new TestCompany(companyId));
        services.AddSingleton(Mock.Of<ICurrentUser>(u => u.UserId == _userId));
        services.AddSingleton<IAuditContext>(new TestAuditContext(_tenantId, companyId, _userId));
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.TodayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Today);
        clock.Setup(c => c.LocalDateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Today);
        services.AddSingleton(clock.Object);
        services.AddAccountingEngine();
        services.AddAccountingContinuity();
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static T Translator<T>(IServiceProvider sp) => ActivatorUtilities.CreateInstance<T>(sp);

    /// <summary>
    /// Transacción operativa simulada: el traductor registra la obligación dentro de ella; tras el
    /// commit se ejecuta el intento inicial con el ejecutor común (mismo orden que producción).
    /// </summary>
    private async Task RecordAsync(Guid companyId, Func<IServiceProvider, Task> handle)
    {
        await using var scope = Scope(companyId);
        var sp = scope.ServiceProvider;
        var unit = sp.GetRequiredService<IUnitOfWork>();
        await unit.BeginTransactionAsync();
        await handle(sp);
        await unit.SaveChangesAsync();
        await unit.CommitAsync();
        await sp.GetRequiredService<IAccountingPostingDispatcher>().DispatchPendingAsync(default);
    }

    private async Task<AccountingPosting> PostingAsync(string module, string fact, Guid sourceId)
    {
        await using var db = Context(_tenantId, _companyId);
        return await db.AccountingPostings.AsNoTracking()
            .SingleAsync(p => p.SourceModule == module && p.FactType == fact && p.SourceEventId == sourceId);
    }

    private async Task<List<JournalEntry>> EntriesAsync(Guid sourceId)
    {
        await using var db = Context(_tenantId, _companyId);
        return await db.JournalEntries.AsNoTracking().Include(j => j.Lines).Where(j => j.SourceEventId == sourceId).ToListAsync();
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
        public Guid TenantId => tenantId;
        public Guid CompanyId => companyId;
        public AuditActor Actor => new(tenantId, userId, "tester", null, null, null, "continuity-04", null, AuditSource.UserAction);
    }

    private sealed class NoPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
