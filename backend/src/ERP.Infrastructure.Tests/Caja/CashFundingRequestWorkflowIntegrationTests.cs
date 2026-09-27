using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Caja.FundingRequests;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Application.Modules.Payables.Services;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Configuration;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — PostgreSQL 16 real, handlers reales, posting real
/// (reglas del bootstrap) y MediatR real: casos A–J del diseño 02E-A + integridad del snapshot,
/// aislamiento empresa/sucursal, ownership del cajero, concurrencia Fulfill/Close y Fulfill/Fulfill,
/// SupplierCredit residual y rollback completo ante fallo de posting.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class CashFundingRequestWorkflowIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_cash_funding_workflow_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    // Misma fecha fija que AlwaysTodayCompanyClock: el bootstrap siembra el período de ese año.
    private static readonly DateOnly Today = new(2026, 9, 17);
    private readonly Guid _cashier = Guid.NewGuid();
    private readonly Guid _requester = Guid.NewGuid();
    private readonly Guid _stranger = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherCompanyId;
    private Guid _branchId;
    private Guid _otherBranchId;
    private Guid _supplierId;
    private Guid _cashMethodId;
    private Guid _bankMethodId;
    private Guid _bankAccountId;
    private Guid _cashRegisterId;
    private Guid _cashSessionId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = PlainContext();
        await db.Database.MigrateAsync();
        await SeedUsersAsync(db);

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _cashier);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _cashier);
        var other = Company.CreateManaged(tenant.Id, "1790098765001", "Otra S.A.", createdBy: _cashier);
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, other);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherCompanyId = other.Id;

        var branch = NewBranch("001", true);
        var otherBranch = NewBranch("002", false);
        var supplier = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Proveedor Test", _cashier);
        db.Branches.AddRange(branch, otherBranch);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;
        _supplierId = supplier.Id;

        await new AccountingBootstrapStep(db, new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance)
            .ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _cashier));
        var accounts = await db.Accounts.Where(a => a.CompanyId == _companyId).ToDictionaryAsync(a => a.Code.Value, a => a.Id);

        var cashMethod = PaymentMethod.Create(_tenantId, "CASH", "Efectivo", false, false, 1, _cashier, affectsPhysicalCash: true);
        var bankMethod = PaymentMethod.Create(_tenantId, "TRANSFER", "Transferencia", true, false, 2, _cashier, PaymentMethodDetailType.Transfer);
        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", _cashier);
        db.PaymentMethods.AddRange(cashMethod, bankMethod);
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        var bankAccount = CompanyBankAccount.Create(_tenantId, _companyId, bank.Id, BankAccountType.Checking,
            "2200123456", "Banco Pichincha CTE", accounts["1.1.02.001"], _cashier);
        db.CompanyBankAccounts.Add(bankAccount);

        var register = CashRegister.Create(_tenantId, _companyId, _branchId, "CAJA-01", "Caja Principal", _cashier);
        register.SetAccountingAccount(accounts["1.1.01.001"], _cashier);
        db.CashRegisters.Add(register);
        var establishment = Establishment.Create(_tenantId, _branchId, _companyId, "001", "Matriz", "Av. Principal 123", null, isMain: true, _cashier);
        db.Set<Establishment>().Add(establishment);
        await db.SaveChangesAsync();
        var emissionPoint = EmissionPoint.Create(_tenantId, _companyId, establishment.Id, "001", "Punto 1",
            ERP.Domain.Modules.Company.Enums.EmissionType.Electronic, isDefault: true, _cashier);
        db.Set<EmissionPoint>().Add(emissionPoint);
        await db.SaveChangesAsync();
        // Caja con $100 operada por el cajero.
        var session = CashSession.Open(_tenantId, _companyId, _branchId, _cashier, register.Id, "CAJA-01",
            "Caja Principal", emissionPoint.Id, "001", 100m, _cashier);
        db.Set<CashSession>().Add(session);
        await db.SaveChangesAsync();

        _cashMethodId = cashMethod.Id;
        _bankMethodId = bankMethod.Id;
        _bankAccountId = bankAccount.Id;
        _cashRegisterId = register.Id;
        _cashSessionId = session.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private Branch NewBranch(string code, bool main) =>
        Branch.Create(
            _tenantId, $"Sucursal {code}", "Av. Principal 123", code, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, main, _cashier,
            companyId: _companyId);

    // ── Contextos ──────────────────────────────────────────────────────────

    private ErpDbContext PlainContext(Guid? companyId = null) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => companyId ?? _companyId));

    /// <summary>Contexto con MediatR real (posting de SupplierPaymentConfirmed).</summary>
    private ErpDbContext WiredContext(Guid? companyId = null)
    {
        var deferred = new DeferredPublisher();
        var company = new FixedCurrentCompany(() => companyId ?? _companyId);
        var db = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            deferred,
            company);

        var services = new ServiceCollection();
        services.AddScoped<ERP.Application.Common.Services.ICompanyClock, ERP.Infrastructure.Persistence.Services.CompanyClock>();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(() => _tenantId));
        services.AddSingleton<ICurrentCompany>(company);
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICompanyBankAccountRepository>(_ => new CompanyBankAccountRepository(db, company));
        services.AddScoped<ICashRegisterRepository>(_ => new CashRegisterRepository(db, company));
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SupplierPaymentConfirmedPostingTranslator).Assembly));
        deferred.Inner = services.BuildServiceProvider().GetRequiredService<IPublisher>();
        return db;
    }

    private SupplierPaymentRegistrar Registrar(ErpDbContext db, Guid companyId)
    {
        var company = new FixedCurrentCompany(() => companyId);
        return new(
            new SupplierPaymentRepository(db),
            new SupplierPaymentSequenceRepository(db),
            new AccountsPayableRepository(db),
            new PaymentMethodRepository(db),
            new CompanyBankAccountRepository(db, company),
            new CashRegisterRepository(db, company),
            new CashSessionRepository(db, company),
            new SupplierCreditRepository(db, company),
            new OperationalPreferencesResolver(
                new OrgSettingsRepository(db, new Mock<IConfigurationChangeLogger>().Object),
                new FixedCurrentTenant(() => _tenantId),
                company,
                NullLogger<OperationalPreferencesResolver>.Instance),
            new CompanyRepository(db));
    }

    private sealed record Actor(Guid UserId, Guid BranchId, Guid CompanyId);

    private Actor Requester => new(_requester, _branchId, _companyId);
    private Actor Cashier => new(_cashier, _branchId, _companyId);

    // ── Ejecución de comandos reales ───────────────────────────────────────

    private async Task<Result<CashFundingRequestDto>> CreateAsync(Actor actor, RegisterSupplierPaymentCommand payment, Guid? clientRequestId = null)
    {
        await using var db = WiredContext(actor.CompanyId);
        var company = new FixedCurrentCompany(() => actor.CompanyId);
        return await new CreateCashFundingRequestHandler(
                new CashFundingRequestRepository(db, company), new CashSessionRepository(db, company),
                Registrar(db, actor.CompanyId), new AllowBranches(_tenantId, actor.CompanyId, actor.UserId, _branchId),
                new UnitOfWork(db), new PostgresDatabaseExceptionTranslator(), new FixedCurrentTenant(() => _tenantId),
                company, new FixedBranch(actor.BranchId), new FixedUser(actor.UserId))
            .Handle(new CreateCashFundingRequestCommand(payment, clientRequestId ?? Guid.NewGuid()), CancellationToken.None);
    }

    private async Task<Result<CashFundingRequestDto>> FulfillAsync(Actor actor, Guid requestId)
    {
        await using var db = WiredContext(actor.CompanyId);
        var company = new FixedCurrentCompany(() => actor.CompanyId);
        return await new FulfillCashFundingRequestHandler(
                new CashFundingRequestRepository(db, company), new CashSessionRepository(db, company),
                Registrar(db, actor.CompanyId), new AllowBranches(_tenantId, actor.CompanyId, actor.UserId, _branchId),
                new UnitOfWork(db), new FixedCurrentTenant(() => _tenantId), company, new FixedBranch(actor.BranchId),
                new FixedUser(actor.UserId))
            .Handle(new FulfillCashFundingRequestCommand(requestId), CancellationToken.None);
    }

    private async Task<Result<CashFundingRequestDto>> RejectAsync(Actor actor, Guid requestId, string reason = "Sin efectivo")
    {
        await using var db = PlainContext(actor.CompanyId);
        var company = new FixedCurrentCompany(() => actor.CompanyId);
        return await new RejectCashFundingRequestHandler(
                new CashFundingRequestRepository(db, company), new CashSessionRepository(db, company),
                new AllowBranches(_tenantId, actor.CompanyId, actor.UserId, _branchId), new UnitOfWork(db),
                new FixedCurrentTenant(() => _tenantId), company, new FixedBranch(actor.BranchId), new FixedUser(actor.UserId))
            .Handle(new RejectCashFundingRequestCommand(requestId, reason), CancellationToken.None);
    }

    private async Task<Result<CashFundingRequestDto>> CancelAsync(Actor actor, Guid requestId)
    {
        await using var db = PlainContext(actor.CompanyId);
        var company = new FixedCurrentCompany(() => actor.CompanyId);
        return await new CancelCashFundingRequestHandler(
                new CashFundingRequestRepository(db, company), new CashSessionRepository(db, company),
                new UnitOfWork(db), new FixedCurrentTenant(() => _tenantId), new FixedUser(actor.UserId))
            .Handle(new CancelCashFundingRequestCommand(requestId, "Ya no se necesita"), CancellationToken.None);
    }

    private async Task<Result<ERP.Application.Modules.Caja.DTOs.CashSessionDto>> CloseAsync(decimal countedCash)
    {
        await using var db = PlainContext();
        var company = new FixedCurrentCompany(() => _companyId);
        return await new CloseCashSessionHandler(
                new CashSessionRepository(db, company), new EmissionPointRepository(db), new CashRegisterRepository(db, company),
                new FixedCurrentTenant(() => _tenantId), new FixedBranch(_branchId), new FixedUser(_cashier),
                new OperationalPreferencesResolver(
                    new OrgSettingsRepository(db, new Mock<IConfigurationChangeLogger>().Object),
                    new FixedCurrentTenant(() => _tenantId), company, NullLogger<OperationalPreferencesResolver>.Instance),
                new CashFundingRequestRepository(db, company), new UnitOfWork(db))
            .Handle(new CloseCashSessionCommand(_cashSessionId, [new CashClosingCountInput(1m, "Billete $1", (int)countedCash)]),
                CancellationToken.None);
    }

    private async Task<Result<SupplierPaymentDto>> PayDirectAsync(Actor actor, RegisterSupplierPaymentCommand payment)
    {
        await using var db = WiredContext(actor.CompanyId);
        return await new RegisterSupplierPaymentCommandHandler(
                Registrar(db, actor.CompanyId), new UnitOfWork(db), new FixedCurrentTenant(() => _tenantId),
                new FixedCurrentCompany(() => actor.CompanyId), new FixedBranch(actor.BranchId), new FixedUser(actor.UserId))
            .Handle(payment, CancellationToken.None);
    }

    // ── Datos ──────────────────────────────────────────────────────────────

    private async Task<Guid> SeedInstallmentAsync(decimal amount)
    {
        await using var db = PlainContext();
        var payable = AccountsPayable.CreateFromOrigin(_tenantId, _companyId, _branchId, _supplierId,
            AccountsPayableOriginType.PurchaseInvoice, Guid.NewGuid(), "01", $"001-001-{Random.Shared.Next(100000, 999999)}",
            Today, Today, _cashier);
        var installment = payable.AddInstallment(1, Today.AddDays(30), amount);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return installment.Id;
    }

    /// <summary>Pago contra una cuota: efectivo de la caja + (opcional) banco.</summary>
    private RegisterSupplierPaymentCommand Payment(Guid installmentId, decimal cash, decimal bank = 0m, decimal? applied = null)
    {
        var lines = new List<SupplierPaymentMethodLineRequest>();
        if (bank > 0)
            lines.Add(new SupplierPaymentMethodLineRequest(_bankMethodId, _bankAccountId, null, bank, "OP-7788", TransactionDate: Today));
        if (cash > 0)
            lines.Add(new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, cash));
        var total = cash + bank;
        var appliedAmount = applied ?? total;
        var allocations = new List<SupplierPaymentAllocationLineRequest>();
        var remaining = appliedAmount;
        for (var i = 0; i < lines.Count && remaining > 0; i++)
        {
            var take = Math.Min(lines[i].Amount, remaining);
            allocations.Add(new SupplierPaymentAllocationLineRequest(i, 0, take));
            remaining -= take;
        }
        return new RegisterSupplierPaymentCommand(_supplierId, Today, total, null, lines,
            [new SupplierPaymentApplicationLineRequest(installmentId, appliedAmount)], allocations,
            ConfirmUnappliedAmount: appliedAmount < total);
    }

    private async Task<(int Payments, int CashMovements, int Journals, decimal Balance, CashFundingRequestStatus? Status)> SnapshotAsync(Guid? requestId = null)
    {
        await using var db = PlainContext();
        var session = await db.Set<CashSession>().Include(s => s.Movements).AsNoTracking().SingleAsync(s => s.Id == _cashSessionId);
        var status = requestId is null
            ? (CashFundingRequestStatus?)null
            : (await db.CashFundingRequests.AsNoTracking().SingleAsync(r => r.Id == requestId)).Status;
        return (
            await db.SupplierPayments.CountAsync(),
            session.Movements.Count(m => m.MovementType == CashMovementType.SupplierPayment),
            await db.JournalEntries.CountAsync(j => j.SourceEventType == "SupplierPaymentConfirmed"),
            session.CurrentBalance,
            status);
    }

    private async Task<Guid> PendingAsync(decimal cash, decimal bank = 0m, decimal? applied = null)
    {
        var installment = await SeedInstallmentAsync(applied ?? cash + bank);
        var created = await CreateAsync(Requester, Payment(installment, cash, bank, applied));
        created.IsSuccess.Should().BeTrue(created.Error);
        return created.Value!.Id;
    }

    // ── A–J ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_duenio_de_caja_paga_directo_y_no_puede_crear_solicitud()
    {
        var installment = await SeedInstallmentAsync(30m);

        var request = await CreateAsync(Cashier, Payment(installment, 30m));
        request.IsSuccess.Should().BeFalse();
        request.Error.Should().Contain("Usted opera esta caja");

        (await PayDirectAsync(Cashier, Payment(installment, 30m))).IsSuccess.Should().BeTrue();
        await using var db = PlainContext();
        var payment = await db.SupplierPayments.AsNoTracking().SingleAsync();
        (payment.CreatedBy, payment.ConfirmedByUserId).Should().Be((_cashier, _cashier));
        (await db.CashFundingRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task B_otro_usuario_crea_Pending_sin_ningun_efecto_financiero()
    {
        var before = await SnapshotAsync();

        var id = await PendingAsync(80m);

        var after = await SnapshotAsync(id);
        after.Status.Should().Be(CashFundingRequestStatus.Pending);
        (after.Payments, after.CashMovements, after.Journals, after.Balance)
            .Should().Be((before.Payments, before.CashMovements, before.Journals, before.Balance));
        await using var db = PlainContext();
        var request = await db.CashFundingRequests.AsNoTracking().SingleAsync(r => r.Id == id);
        (request.RequestedByUserId, request.CashAmount, request.BranchId, request.CashSessionId)
            .Should().Be((_requester, 80m, _branchId, _cashSessionId));
        (await db.Set<SupplierCredit>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task B_crear_con_saldo_insuficiente_se_rechaza_sin_reservar()
    {
        var installment = await SeedInstallmentAsync(150m);

        var result = await CreateAsync(Requester, Payment(installment, 150m));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("dispone de $100.00");
    }

    [Fact]
    public async Task C_cajero_rechaza_sin_efectos()
    {
        var id = await PendingAsync(40m);

        var rejected = await RejectAsync(Cashier, id);

        rejected.IsSuccess.Should().BeTrue(rejected.Error);
        (rejected.Value!.Status, rejected.Value.ResolvedByUserId, rejected.Value.ResolutionReason)
            .Should().Be(("Rejected", (Guid?)_cashier, "Sin efectivo"));
        (await SnapshotAsync()).Payments.Should().Be(0);
        (await RejectAsync(Cashier, id)).IsSuccess.Should().BeFalse("terminal");
    }

    [Fact]
    public async Task D_cerrar_caja_cancela_las_Pending_y_no_toca_las_terminales()
    {
        var pending = await PendingAsync(40m);
        var rejected = await PendingAsync(20m);
        (await RejectAsync(Cashier, rejected)).IsSuccess.Should().BeTrue();

        var closed = await CloseAsync(100m);

        closed.IsSuccess.Should().BeTrue(closed.Error);
        await using var db = PlainContext();
        var requests = await db.CashFundingRequests.AsNoTracking().ToDictionaryAsync(r => r.Id);
        (requests[pending].Status, requests[pending].ResolutionReason, requests[pending].ResolvedByUserId)
            .Should().Be((CashFundingRequestStatus.Cancelled, "Caja cerrada", (Guid?)_cashier));
        (requests[rejected].Status, requests[rejected].ResolutionReason)
            .Should().Be((CashFundingRequestStatus.Rejected, "Sin efectivo"));
        (await FulfillAsync(Cashier, pending)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task E_saldo_consumido_antes_de_atender_revierte_y_la_solicitud_sigue_Pending()
    {
        var id = await PendingAsync(80m);
        (await PayDirectAsync(Cashier, Payment(await SeedInstallmentAsync(50m), 50m))).IsSuccess.Should().BeTrue();

        var result = await FulfillAsync(Cashier, id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("dispone de $50.00");
        var state = await SnapshotAsync(id);
        (state.Status, state.Payments, state.CashMovements, state.Balance)
            .Should().Be((CashFundingRequestStatus.Pending, 1, 1, 50m));
    }

    [Fact]
    public async Task F_J_pago_mixto_banco_120_caja_80_crea_un_solo_pago_un_asiento_y_un_egreso_del_cajero()
    {
        var id = await PendingAsync(80m, bank: 120m);

        var result = await FulfillAsync(Cashier, id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Fulfilled");
        var state = await SnapshotAsync(id);
        (state.Payments, state.CashMovements, state.Journals, state.Balance).Should().Be((1, 1, 1, 20m));

        await using var db = PlainContext();
        var payment = await db.SupplierPayments.Include(p => p.MethodLines).AsNoTracking().SingleAsync();
        payment.Id.Should().Be(result.Value.SupplierPaymentId!.Value);
        payment.TotalAmount.Should().Be(200m);
        payment.MethodLines.Should().HaveCount(2);
        (payment.CreatedBy, payment.ConfirmedByUserId).Should().Be((_requester, _cashier));
        var movement = await db.CashMovements.AsNoTracking().SingleAsync(m => m.ReferenceId == payment.Id);
        (movement.CreatedBy, movement.Amount).Should().Be((_cashier, 80m));
        var request = await db.CashFundingRequests.AsNoTracking().SingleAsync(r => r.Id == id);
        (request.RequestedByUserId, request.ResolvedByUserId).Should().Be((_requester, (Guid?)_cashier));
        var journal = await db.JournalEntries.Include(j => j.Lines).AsNoTracking().SingleAsync(j => j.SourceEventId == payment.Id);
        journal.Lines.Sum(l => l.Debit).Should().Be(journal.Lines.Sum(l => l.Credit)).And.Be(200m);
    }

    [Fact]
    public async Task G_dos_solicitudes_misma_caja_se_serializan_sin_sobregiro()
    {
        var a = await PendingAsync(80m);
        var b = await PendingAsync(50m);

        var results = await Task.WhenAll(Task.Run(() => FulfillAsync(Cashier, a)), Task.Run(() => FulfillAsync(Cashier, b)));

        results.Count(r => r.IsSuccess).Should().Be(1, "$100 no alcanzan para $80 + $50");
        results.Single(r => !r.IsSuccess).Error.Should().Contain("dispone de");
        var state = await SnapshotAsync();
        state.Payments.Should().Be(1);
        state.Balance.Should().BeGreaterThanOrEqualTo(0m);
        await using var db = PlainContext();
        (await db.CashFundingRequests.CountAsync(r => r.Status == CashFundingRequestStatus.Pending))
            .Should().Be(1, "la que no alcanzó sigue Pending, sin reserva silenciosa");
    }

    [Fact]
    public async Task H_solicitud_atendida_no_se_reutiliza_y_el_reintento_no_duplica_el_pago()
    {
        var id = await PendingAsync(30m);
        var first = await FulfillAsync(Cashier, id);
        first.IsSuccess.Should().BeTrue(first.Error);

        var retry = await FulfillAsync(Cashier, id);

        retry.IsSuccess.Should().BeTrue();
        retry.Value!.SupplierPaymentId.Should().Be(first.Value!.SupplierPaymentId);
        (await SnapshotAsync()).Payments.Should().Be(1, "el reintento devuelve el mismo pago, no crea otro");
        (await RejectAsync(Cashier, id)).IsSuccess.Should().BeFalse();
        (await CancelAsync(Requester, id)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task I_solo_el_solicitante_cancela()
    {
        var id = await PendingAsync(30m);

        var byOther = await CancelAsync(new Actor(_stranger, _branchId, _companyId), id);
        byOther.IsSuccess.Should().BeFalse();
        byOther.Code.Should().Be(ApiResponseCodes.Common.Forbidden);

        var byRequester = await CancelAsync(Requester, id);
        byRequester.IsSuccess.Should().BeTrue(byRequester.Error);
        (byRequester.Value!.Status, byRequester.Value.ResolvedByUserId).Should().Be(("Cancelled", (Guid?)_requester));
        (await FulfillAsync(Cashier, id)).IsSuccess.Should().BeFalse();
    }

    // ── Integridad / aislamiento / ownership ──────────────────────────────

    [Theory]
    [InlineData("UPDATE cash_funding_requests SET payment_payload = jsonb_set(payment_payload, '{totalAmount}', '999') WHERE id = @id")]
    [InlineData("UPDATE cash_funding_requests SET payload_version = 2 WHERE id = @id")]
    public async Task Snapshot_manipulado_o_de_version_desconocida_falla_cerrado(string sql)
    {
        var id = await PendingAsync(30m);
        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
        }

        var result = await FulfillAsync(Cashier, id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no es íntegra");
        var state = await SnapshotAsync(id);
        (state.Status, state.Payments, state.CashMovements).Should().Be((CashFundingRequestStatus.Pending, 0, 0));
    }

    [Fact]
    public async Task Otra_empresa_no_puede_atender_ni_ver_la_solicitud()
    {
        var id = await PendingAsync(30m);

        var result = await FulfillAsync(new Actor(_cashier, _branchId, _otherCompanyId), id);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await SnapshotAsync(id)).Status.Should().Be(CashFundingRequestStatus.Pending);
    }

    [Fact]
    public async Task Otra_sucursal_no_puede_crear_ni_atender()
    {
        var installment = await SeedInstallmentAsync(30m);
        var create = await CreateAsync(new Actor(_requester, _otherBranchId, _companyId), Payment(installment, 30m));
        create.IsSuccess.Should().BeFalse();
        create.Error.Should().Contain("sucursal activa");

        var id = await PendingAsync(30m);
        var fulfill = await FulfillAsync(new Actor(_cashier, _otherBranchId, _companyId), id);
        fulfill.IsSuccess.Should().BeFalse();
        fulfill.Error.Should().Contain("sucursal activa");
        (await SnapshotAsync(id)).Status.Should().Be(CashFundingRequestStatus.Pending);
    }

    [Fact]
    public async Task Quien_no_controla_la_caja_no_puede_atender_ni_rechazar()
    {
        var id = await PendingAsync(30m);
        var stranger = new Actor(_stranger, _branchId, _companyId);

        (await FulfillAsync(stranger, id)).Error.Should().Be("La caja seleccionada está siendo operada por otro usuario.");
        (await RejectAsync(stranger, id)).Error.Should().Be("La caja seleccionada está siendo operada por otro usuario.");
        (await FulfillAsync(Requester, id)).IsSuccess.Should().BeFalse("el solicitante tampoco controla la caja");
        (await SnapshotAsync(id)).Status.Should().Be(CashFundingRequestStatus.Pending);
    }

    [Fact]
    public async Task ClientRequestId_repetido_devuelve_la_misma_solicitud_o_conflicto()
    {
        var installment = await SeedInstallmentAsync(30m);
        var clientRequestId = Guid.NewGuid();
        var first = await CreateAsync(Requester, Payment(installment, 30m), clientRequestId);
        first.IsSuccess.Should().BeTrue(first.Error);

        var same = await CreateAsync(Requester, Payment(installment, 30m), clientRequestId);
        same.IsSuccess.Should().BeTrue();
        same.Value!.Id.Should().Be(first.Value!.Id);

        var different = await CreateAsync(Requester, Payment(installment, 20m), clientRequestId);
        different.IsSuccess.Should().BeFalse();
        different.Code.Should().Be(ApiResponseCodes.Common.Conflict);

        await using var db = PlainContext();
        (await db.CashFundingRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Close_y_Fulfill_concurrentes_se_serializan_por_el_lock_de_la_caja()
    {
        var id = await PendingAsync(30m);

        var fulfill = Task.Run(() => FulfillAsync(Cashier, id));
        var close = Task.Run(() => CloseAsync(100m));
        await Task.WhenAll(fulfill, close);

        var state = await SnapshotAsync(id);
        if (state.Status == CashFundingRequestStatus.Fulfilled)
            state.Payments.Should().Be(1, "se atendió antes del cierre");
        else
        {
            state.Status.Should().Be(CashFundingRequestStatus.Cancelled, "se cerró antes de atender");
            state.Payments.Should().Be(0);
        }
        (fulfill.Result.IsSuccess && state.Status == CashFundingRequestStatus.Cancelled).Should().BeFalse();
    }

    [Fact]
    public async Task Remanente_confirmado_genera_SupplierCredit_del_originador()
    {
        var id = await PendingAsync(50m, applied: 30m);

        var result = await FulfillAsync(Cashier, id);

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var db = PlainContext();
        var credit = await db.Set<SupplierCredit>().AsNoTracking().SingleAsync();
        (credit.SourceSupplierPaymentId, credit.OriginalAmount, credit.CreatedBy)
            .Should().Be((result.Value!.SupplierPaymentId, 20m, _requester));
    }

    [Fact]
    public async Task Fallo_de_posting_revierte_todo_y_la_solicitud_sigue_Pending()
    {
        var id = await PendingAsync(30m);
        await using (var db = PlainContext())
        {
            var rule = await db.PostingRules.SingleAsync(r => r.SourceModule == "Payables" && r.FactType == "SupplierPaymentConfirmed");
            rule.Disable(_cashier);
            await db.SaveChangesAsync();
        }

        var result = await FulfillAsync(Cashier, id);

        result.IsSuccess.Should().BeFalse();
        var state = await SnapshotAsync(id);
        (state.Status, state.Payments, state.CashMovements, state.Journals, state.Balance)
            .Should().Be((CashFundingRequestStatus.Pending, 0, 0, 0, 100m));
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

    private sealed class AllowBranches(Guid tenantId, Guid companyId, Guid userId, Guid allowedBranchId) : IBranchAccessGuard
    {
        public Task<Result<BranchAccessContext>> RequireBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(branchId == allowedBranchId
                ? Result<BranchAccessContext>.Success(new BranchAccessContext(userId, tenantId, companyId, branchId, "Matriz", true))
                : Result<BranchAccessContext>.Forbidden("Sin acceso a la sucursal."));
    }

    private sealed class FixedBranch(Guid branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId;
        public bool IsAuthenticated => true;
        public bool HasBranchContext => branchId != Guid.Empty;
    }

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
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
}
