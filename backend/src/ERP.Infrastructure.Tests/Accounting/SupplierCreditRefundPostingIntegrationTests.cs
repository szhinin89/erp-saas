using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Tests.Audit;
using ERP.Infrastructure.Tests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — PostgreSQL 16 real + PostingEngine real + MediatR con
/// escaneo de ensamblado (mismo registro que producción) + reglas sembradas por el
/// <see cref="AccountingBootstrapStep"/> real (empresa nueva). Cubre: asiento del reembolso
/// banco/caja (Debe cuenta real del destino, Haber 1.1.03.004 de la regla), reembolso parcial,
/// reversa espejo con la cuenta congelada aunque el destino cambie de cuenta, CashMovement sin
/// segundo asiento, y rollback total (saldo, transacción, caja, asiento) si el posting falla.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierCreditRefundPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_supplier_credit_refund_posting_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    // Misma fecha fija que AlwaysTodayCompanyClock: el bootstrap siembra el período de ese año.
    private readonly DateOnly _today = new(2026, 9, 17);
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _bankMethodId;
    private Guid _companyBankAccountId;
    private Guid _cashRegisterId;
    private Guid _cashSessionId;
    private Guid _bankLedgerAccountId;
    private Guid _bankSavingsLedgerAccountId;
    private Guid _pettyCashLedgerAccountId;
    private Guid _advancesLedgerAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            _tenantId, "Matriz", "Av. Principal 123", "001", null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, true, _userId,
            companyId: _companyId
        );
        var supplier = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Proveedor Test", _userId);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
        _branchId = branch.Id;
        _supplierId = supplier.Id;

        // Empresa nueva: plan de cuentas + período + PostingRules por el bootstrap REAL (incluye las
        // reglas canónicas SupplierCreditRefunded/SupplierCreditRefundReversed de 02D-B).
        await new AccountingBootstrapStep(db, new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance)
            .ExecuteAsync(new ERP.Application.Common.Interfaces.CompanyBootstrapContext(_tenantId, _companyId, _userId));

        var accounts = await db.Accounts.Where(a => a.CompanyId == _companyId).ToDictionaryAsync(a => a.Code.Value, a => a.Id);
        _bankLedgerAccountId = accounts["1.1.02.001"];
        _bankSavingsLedgerAccountId = accounts["1.1.02.002"];
        _pettyCashLedgerAccountId = accounts["1.1.01.002"];
        _advancesLedgerAccountId = accounts["1.1.03.004"];

        var cashMethod = PaymentMethod.Create(_tenantId, "CASH", "Efectivo", false, false, 1, _userId, affectsPhysicalCash: true);
        var bankMethod = PaymentMethod.Create(_tenantId, "TRANSFER", "Transferencia", true, false, 2, _userId, PaymentMethodDetailType.Transfer);
        db.PaymentMethods.AddRange(cashMethod, bankMethod);

        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", _userId);
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        _bankMethodId = bankMethod.Id;

        var companyBankAccount = CompanyBankAccount.Create(
            _tenantId, _companyId, bank.Id, BankAccountType.Checking, "2200123456", "Banco Pichincha CTE",
            _bankLedgerAccountId, _userId
        );
        db.CompanyBankAccounts.Add(companyBankAccount);

        // Caja con cuenta propia distinta de "Caja general": prueba que el Debe sale de la caja real.
        var cashRegister = CashRegister.Create(_tenantId, _companyId, _branchId, "CAJA-02", "Caja Chica", _userId);
        cashRegister.SetAccountingAccount(_pettyCashLedgerAccountId, _userId);
        db.CashRegisters.Add(cashRegister);

        var establishment = Establishment.Create(_tenantId, _branchId, _companyId, "001", "Matriz", "Av. Principal 123", null, isMain: true, _userId);
        db.Set<Establishment>().Add(establishment);
        await db.SaveChangesAsync();
        var emissionPoint = EmissionPoint.Create(
            _tenantId, _companyId, establishment.Id, "001", "Punto 1",
            ERP.Domain.Modules.Company.Enums.EmissionType.Electronic, isDefault: true, _userId
        );
        db.Set<EmissionPoint>().Add(emissionPoint);
        await db.SaveChangesAsync();

        var session = CashSession.Open(
            _tenantId, _companyId, _branchId, _userId, cashRegister.Id, "CAJA-02", "Caja Chica",
            emissionPoint.Id, "001", 500m, _userId
        );
        db.Set<CashSession>().Add(session);
        await db.SaveChangesAsync();

        _companyBankAccountId = companyBankAccount.Id;
        _cashRegisterId = cashRegister.Id;
        _cashSessionId = session.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            publisher ?? new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );

    /// <summary>Mismo mecanismo que PurchaseCreditNoteDiscountPostingIntegrationTests.BuildWiredContext.</summary>
    private ErpDbContext BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var db = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            deferred,
            new FixedCurrentCompany(() => _companyId)
        );

        var company = new FixedCurrentCompany(() => _companyId);
        var services = new ServiceCollection();
        services.AddScoped<ICompanyClock, ERP.Infrastructure.Persistence.Services.CompanyClock>();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(() => _tenantId));
        services.AddSingleton<ICurrentCompany>(company);
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddScoped<ISupplierCreditRepository>(_ => new SupplierCreditRepository(db, company));
        services.AddScoped<ISupplierCreditRefundTransactionRepository>(_ => new SupplierCreditRefundTransactionRepository(db, company));
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(() => _tenantId, () => _companyId, _userId));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SupplierCreditRefundedPostingTranslator).Assembly));

        deferred.Inner = services.BuildServiceProvider().GetRequiredService<IPublisher>();
        return db;
    }

    private RegisterSupplierCreditRefundHandler RegisterHandler(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(() => _companyId);
        return new(
            new SupplierCreditRepository(db, company),
            new SupplierCreditRefundTransactionRepository(db, company),
            new CompanyBankAccountRepository(db, company),
            new CashRegisterRepository(db, company),
            new AccountRepository(db),
            new PaymentMethodRepository(db),
            new CashSessionRepository(db, company),
            new CompanyRepository(db),
            new UnitOfWork(db),
            new PostgresDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
    }

    private ReverseSupplierCreditRefundHandler ReverseHandler(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(() => _companyId);
        return new(
            new SupplierCreditRepository(db, company),
            new SupplierCreditRefundTransactionRepository(db, company),
            new CashSessionRepository(db, company),
            new UnitOfWork(db),
            new PostgresDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
    }

    /// <summary>Anticipo real: pago a proveedor sin CxP (100% remanente) → SupplierCredit origen SupplierPayment.</summary>
    private async Task<Guid> SeedAdvanceAsync(decimal amount)
    {
        await using var db = CreateContext();
        var payment = SupplierPayment.Create(
            _tenantId, _companyId, _branchId, _supplierId, _today, amount,
            $"SP-{Guid.NewGuid():N}"[..12], null,
            [new SupplierPaymentMethodLineInput(_bankMethodId, _companyBankAccountId, null, amount, "OP-1", TransactionDate: _today)],
            [],
            [],
            _userId,
            unappliedAmountConfirmed: true,
            allowWithoutPayable: true
        );
        var credit = SupplierCredit.CreateFromSupplierPayment(
            _tenantId, _companyId, _branchId, _supplierId, "USD", payment.Id, payment.UnappliedAmount, _userId);
        db.SupplierPayments.Add(payment);
        db.Set<SupplierCredit>().Add(credit);
        await db.SaveChangesAsync();
        return credit.Id;
    }

    private async Task<Result<SupplierCreditRefundTransactionDto>> RefundAsync(
        Guid creditId, decimal amount, bool cash, string? reference = "TRX-778899")
    {
        await using var db = BuildWiredContext();
        return await RegisterHandler(db).HandleWithDomainRules(
            new RegisterSupplierCreditRefundCommand(
                creditId,
                cash ? null : _companyBankAccountId,
                cash ? _cashRegisterId : null,
                cash ? "CASH" : "TRANSFER",
                amount,
                _today,
                reference,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );
    }

    private async Task<Result<SupplierCreditRefundTransactionDto>> ReverseAsync(Guid creditId, Guid refundId)
    {
        await using var db = BuildWiredContext();
        return await ReverseHandler(db).HandleWithDomainRules(
            new ReverseSupplierCreditRefundCommand(creditId, refundId, "Transferencia devuelta por el banco", _today, Guid.NewGuid()),
            CancellationToken.None
        );
    }

    private async Task<List<(Guid AccountId, decimal Debit, decimal Credit)>> JournalLinesAsync(Guid sourceEventId, string factType)
    {
        await using var db = CreateContext();
        var entries = await db.JournalEntries.Include(j => j.Lines)
            .Where(j => j.SourceEventId == sourceEventId && j.SourceModule == "Purchases" && j.SourceEventType == factType)
            .ToListAsync();
        entries.Should().ContainSingle($"exactamente un asiento {factType} por movimiento");
        entries[0].EntryDate.Should().Be(_today);
        return entries[0].Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).ToList();
    }

    private async Task<Guid> RefundMovementIdAsync(Guid refundTransactionId)
    {
        await using var db = CreateContext();
        return (await db.SupplierCreditRefundTransactions.AsNoTracking().SingleAsync(t => t.Id == refundTransactionId))
            .SupplierCreditMovementId;
    }

    private async Task<decimal> AvailableAsync(Guid creditId)
    {
        await using var db = CreateContext();
        return (await db.Set<SupplierCredit>().AsNoTracking().SingleAsync(c => c.Id == creditId)).AvailableAmount;
    }

    [Fact]
    public async Task Reembolso_banco_parcial_Debe_banco_real_Haber_anticipos_de_la_regla()
    {
        var creditId = await SeedAdvanceAsync(100m);

        var result = await RefundAsync(creditId, 40m, cash: false);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await AvailableAsync(creditId)).Should().Be(60m, "reembolso parcial");
        var lines = await JournalLinesAsync(await RefundMovementIdAsync(result.Value!.Id), "SupplierCreditRefunded");
        lines.Should().BeEquivalentTo(new[]
        {
            (_bankLedgerAccountId, 40m, 0m),
            (_advancesLedgerAccountId, 0m, 40m),
        });

        await using var db = CreateContext();
        var ruleAccount = (await db.PostingRules.Include(r => r.Lines)
            .SingleAsync(r => r.SourceModule == "Purchases" && r.FactType == "SupplierCreditRefunded")).Lines.Single().AccountId;
        ruleAccount.Should().Be(_advancesLedgerAccountId, "la cuenta de anticipos sale de la PostingRule (SSOT), no del código");
        var tx = await db.SupplierCreditRefundTransactions.AsNoTracking().SingleAsync(t => t.Id == result.Value.Id);
        (tx.CompanyBankAccountId, tx.EffectiveDate, tx.Amount, tx.PaymentMethodCode, tx.ExternalReference, tx.DestinationCodeSnapshot)
            .Should().Be((_companyBankAccountId, _today, 40m, "TRANSFER", "TRX-778899", "2200123456"));
    }

    [Fact]
    public async Task Reembolso_caja_Debe_la_cuenta_de_la_caja_y_el_CashMovement_no_genera_segundo_asiento()
    {
        var creditId = await SeedAdvanceAsync(100m);
        int journalsBefore;
        await using (var db = CreateContext())
            journalsBefore = await db.JournalEntries.CountAsync();

        var result = await RefundAsync(creditId, 30m, cash: true, reference: null);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CashMovementId.Should().NotBeNull();
        var lines = await JournalLinesAsync(await RefundMovementIdAsync(result.Value.Id), "SupplierCreditRefunded");
        lines.Should().BeEquivalentTo(new[]
        {
            (_pettyCashLedgerAccountId, 30m, 0m),
            (_advancesLedgerAccountId, 0m, 30m),
        });
        await using var verify = CreateContext();
        (await verify.JournalEntries.CountAsync()).Should().Be(journalsBefore + 1, "el CashMovement nunca contabiliza por sí mismo");
        (await verify.JournalEntries.AnyAsync(j => j.SourceEventId == result.Value.CashMovementId!.Value)).Should().BeFalse();
        var cashMovement = await verify.Set<CashMovement>().AsNoTracking().SingleAsync(m => m.Id == result.Value.CashMovementId!.Value);
        cashMovement.MovementType.Should().Be(CashMovementType.ManualIncome);
        cashMovement.Amount.Should().Be(30m);
    }

    [Fact]
    public async Task Reversa_banco_es_espejo_exacto_con_la_cuenta_congelada_aunque_el_banco_cambie_de_cuenta()
    {
        var creditId = await SeedAdvanceAsync(100m);
        var refund = await RefundAsync(creditId, 40m, cash: false);
        refund.IsSuccess.Should().BeTrue(refund.Error);

        await using (var db = CreateContext())
        {
            var bankAccount = await db.CompanyBankAccounts.SingleAsync(b => b.Id == _companyBankAccountId);
            bankAccount.Update(bankAccount.DisplayName, _bankSavingsLedgerAccountId, _userId);
            await db.SaveChangesAsync();
        }

        // Por Id del MOVIMIENTO (lo que envía la UI por /refund/{movementId}/reverse).
        var reversed = await ReverseAsync(creditId, await RefundMovementIdAsync(refund.Value!.Id));

        reversed.IsSuccess.Should().BeTrue(reversed.Error);
        (await AvailableAsync(creditId)).Should().Be(100m);
        var lines = await JournalLinesAsync(await RefundMovementIdAsync(reversed.Value!.Id), "SupplierCreditRefundReversed");
        lines.Should().BeEquivalentTo(new[]
        {
            (_advancesLedgerAccountId, 40m, 0m),
            (_bankLedgerAccountId, 0m, 40m),
        }, "nunca se resuelve la cuenta vigente del banco al reversar");
        await using var verify = CreateContext();
        (await verify.SupplierCreditRefundTransactions.CountAsync(t => t.SupplierCreditId == creditId))
            .Should().Be(2, "el reembolso original nunca se borra; la reversa es una fila nueva");
    }

    [Fact]
    public async Task Reversa_caja_es_espejo_exacto_y_egresa_de_la_misma_caja()
    {
        var creditId = await SeedAdvanceAsync(100m);
        var refund = await RefundAsync(creditId, 25m, cash: true, reference: null);
        refund.IsSuccess.Should().BeTrue(refund.Error);

        var reversed = await ReverseAsync(creditId, refund.Value!.Id);

        reversed.IsSuccess.Should().BeTrue(reversed.Error);
        reversed.Value!.CashSessionId.Should().Be(_cashSessionId);
        (await AvailableAsync(creditId)).Should().Be(100m);
        var lines = await JournalLinesAsync(await RefundMovementIdAsync(reversed.Value.Id), "SupplierCreditRefundReversed");
        lines.Should().BeEquivalentTo(new[]
        {
            (_advancesLedgerAccountId, 25m, 0m),
            (_pettyCashLedgerAccountId, 0m, 25m),
        });
        await using var verify = CreateContext();
        var expense = await verify.Set<CashMovement>().AsNoTracking().SingleAsync(m => m.Id == reversed.Value.CashMovementId!.Value);
        expense.MovementType.Should().Be(CashMovementType.ManualExpense);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Posting_fallido_revierte_todo_sin_saldo_transaccion_caja_ni_asiento(bool cash)
    {
        var creditId = await SeedAdvanceAsync(100m);
        await using (var db = CreateContext())
        {
            var rule = await db.PostingRules.SingleAsync(r => r.SourceModule == "Purchases" && r.FactType == "SupplierCreditRefunded");
            rule.Disable(_userId);
            await db.SaveChangesAsync();
        }
        int cashMovementsBefore, journalsBefore;
        await using (var db = CreateContext())
        {
            cashMovementsBefore = await db.Set<CashMovement>().CountAsync();
            journalsBefore = await db.JournalEntries.CountAsync();
        }

        var result = await RefundAsync(creditId, 40m, cash);

        result.IsSuccess.Should().BeFalse("sin asiento no hay reembolso (fail-closed)");
        await using var verify = CreateContext();
        (await verify.Set<SupplierCredit>().AsNoTracking().SingleAsync(c => c.Id == creditId)).AvailableAmount.Should().Be(100m);
        (await verify.Set<SupplierCreditMovement>().CountAsync(m => m.SupplierCreditId == creditId)).Should().Be(0);
        (await verify.SupplierCreditRefundTransactions.CountAsync(t => t.SupplierCreditId == creditId)).Should().Be(0);
        (await verify.Set<CashMovement>().CountAsync()).Should().Be(cashMovementsBefore);
        (await verify.JournalEntries.CountAsync()).Should().Be(journalsBefore);
    }

    [Fact]
    public async Task Medio_incoherente_con_el_destino_se_rechaza_sin_efectos()
    {
        var creditId = await SeedAdvanceAsync(100m);
        await using var db = BuildWiredContext();

        var result = await RegisterHandler(db).HandleWithDomainRules(
            new RegisterSupplierCreditRefundCommand(creditId, _companyBankAccountId, null, "CASH", 40m, _today, null, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("efectivo físico");
        (await AvailableAsync(creditId)).Should().Be(100m);
    }

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Inner!.Publish(notification, cancellationToken);
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }
}
