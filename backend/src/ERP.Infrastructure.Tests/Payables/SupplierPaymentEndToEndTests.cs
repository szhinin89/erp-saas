using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Payables;

/// <summary>
/// SUPPLIER-PAYMENTS-CLOSEOUT-15F — suite de integración (PostgreSQL 16 real vía Testcontainers)
/// para el flujo completo de Pagos a Proveedores: RegisterSupplierPaymentCommand →
/// SupplierPayment.Create() → AccountsPayable.RegisterPaymentToInstallment() →
/// SupplierPaymentConfirmedEvent → SupplierPaymentConfirmedPostingTranslator → IPostingEngine →
/// JournalEntry, todo en la transacción explícita del handler. Mismo patrón de DI real (AddMediatR
/// con escaneo de ensamblado) que <c>CollectionPostingIntegrationTests</c> — sin mocks de EF Core.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierPaymentEndToEndTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_supplier_payment_e2e_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _createdBy;

    private Guid _purchasePayableId;
    private Guid _purchaseInstallmentId;
    private Guid _expensePayableId;
    private Guid _expenseInstallmentId;

    private Guid _cashMethodId;
    private Guid _transferMethodId;
    private Guid _cashRegisterId;
    private Guid _cashSessionId;
    private const decimal OpeningCash = 1000m;
    private Guid _companyBankAccountId;
    private Guid _cashAccountId;
    private Guid _bankAccountId;
    private Guid _payablesAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            _createdBy,
            companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _supplierId = supplier.Id;

        // ── AccountsPayable pendiente de Compras (1 cuota) ──
        var purchasePayable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            _createdBy
        );
        var purchaseInstallment = purchasePayable.AddInstallment(1, new DateOnly(2026, 9, 1), 300m);

        // ── AccountsPayable pendiente de Gastos (1 cuota) ──
        var expensePayable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.ExpenseDocument,
            Guid.NewGuid(),
            "EXP",
            "EXP-000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            _createdBy
        );
        var expenseInstallment = expensePayable.AddInstallment(1, new DateOnly(2026, 9, 1), 200m);

        db.AccountsPayables.AddRange(purchasePayable, expensePayable);
        await db.SaveChangesAsync();

        _purchasePayableId = purchasePayable.Id;
        _purchaseInstallmentId = purchaseInstallment.Id;
        _expensePayableId = expensePayable.Id;
        _expenseInstallmentId = expenseInstallment.Id;

        // ── Catálogos: medios de pago + destinos financieros con cuenta contable ──
        var cashMethod = PaymentMethod.Create(
            _tenantId,
            "EFEC",
            "Efectivo",
            false,
            false,
            1,
            _createdBy,
            affectsPhysicalCash: true
        );
        var transferMethod = PaymentMethod.Create(
            _tenantId,
            "TRANS",
            "Transferencia",
            true,
            false,
            2,
            _createdBy,
            PaymentMethodDetailType.Transfer
        );
        db.PaymentMethods.AddRange(cashMethod, transferMethod);

        var cashAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]),
            "Caja",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var bankAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]),
            "Banco",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var payablesAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]),
            "Cuentas por pagar proveedores",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(cashAccount, bankAccount, payablesAccount);
        await db.SaveChangesAsync();

        var cashRegisterEntity = CashRegister.Create(
            _tenantId,
            _companyId,
            _branchId,
            "CAJA-01",
            "Caja Principal",
            _createdBy
        );
        cashRegisterEntity.SetAccountingAccount(cashAccount.Id, _createdBy);
        db.CashRegisters.Add(cashRegisterEntity);
        await db.SaveChangesAsync();

        // 02A — un pago en efectivo exige la CashSession Open de su caja.
        var establishment = Establishment.Create(
            _tenantId,
            _branchId,
            _companyId,
            "001",
            "Matriz",
            "Av. Principal 123",
            null,
            isMain: true,
            _createdBy
        );
        db.Set<Establishment>().Add(establishment);
        await db.SaveChangesAsync();
        var emissionPoint = EmissionPoint.Create(
            _tenantId,
            _companyId,
            establishment.Id,
            "001",
            "Punto de emisión 1",
            ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            _createdBy
        );
        db.Set<EmissionPoint>().Add(emissionPoint);
        await db.SaveChangesAsync();
        var cashSession = CashSession.Open(
            _tenantId,
            _companyId,
            _branchId,
            _createdBy,
            cashRegisterEntity.Id,
            "CAJA-01",
            "Caja Principal",
            emissionPoint.Id,
            "001",
            OpeningCash,
            _createdBy
        );
        db.Set<CashSession>().Add(cashSession);
        await db.SaveChangesAsync();
        _cashSessionId = cashSession.Id;

        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", _createdBy);
        db.Banks.Add(bank);
        await db.SaveChangesAsync();

        var companyBankAccount = CompanyBankAccount.Create(
            _tenantId,
            _companyId,
            bank.Id,
            BankAccountType.Checking,
            "2200123456",
            "Banco Pichincha",
            bankAccount.Id,
            _createdBy
        );
        db.CompanyBankAccounts.Add(companyBankAccount);
        await db.SaveChangesAsync();

        _cashMethodId = cashMethod.Id;
        _transferMethodId = transferMethod.Id;
        _cashRegisterId = cashRegisterEntity.Id;
        _companyBankAccountId = companyBankAccount.Id;
        _cashAccountId = cashAccount.Id;
        _bankAccountId = bankAccount.Id;
        _payablesAccountId = payablesAccount.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            // Igual que producción (DependencyInjection): corrige hijos nuevos (p. ej. CashMovement
            // agregado a una CashSession ya trackeada) descubiertos como Modified por fixup.
            .AddInterceptors(new NewChildEntityTrackingInterceptor())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            publisher ?? new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    /// <summary>Mismo mecanismo de producción (AddMediatR con escaneo de ensamblado) que
    /// CollectionPostingIntegrationTests — confirma que SupplierPaymentConfirmedPostingTranslator
    /// se registra automáticamente como INotificationHandler&lt;SupplierPaymentConfirmedEvent&gt;.</summary>
    private (ErpDbContext db, IPublisher publisher) BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .AddInterceptors(new NewChildEntityTrackingInterceptor())
            .Options;
        var db = new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            deferred,
            new FixedCurrentCompany(_companyId)
        );

        var services = new ServiceCollection();
        services.AddScoped<ERP.Application.Common.Services.ICompanyClock, ERP.Infrastructure.Persistence.Services.CompanyClock>();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(_companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICompanyBankAccountRepository, CompanyBankAccountRepository>();
        services.AddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SupplierPaymentConfirmedPostingTranslator).Assembly)
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    private async Task SeedPostingRuleAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
    {
        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Payables",
            "SupplierPaymentConfirmed",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(_payablesAccountId, AccountNature.Debit, PostingAmountKind.GrandTotal);

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(
                entryDate.Year,
                entryDate.Month,
                DateTime.DaysInMonth(entryDate.Year, entryDate.Month)
            ),
            _createdBy
        );

        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private RegisterSupplierPaymentCommandHandler BuildHandler(ErpDbContext db) =>
        new(
            new SupplierPaymentRepository(db),
            new SupplierPaymentSequenceRepository(db),
            new AccountsPayableRepository(db),
            new PaymentMethodRepository(db),
            new CompanyBankAccountRepository(db, new FixedCurrentCompany(_companyId)),
            new CashRegisterRepository(db, new FixedCurrentCompany(_companyId)),
            new CashSessionRepository(db, new FixedCurrentCompany(_companyId)),
            new UnitOfWork(db),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_createdBy)
        );

    private ReverseSupplierPaymentCommandHandler BuildReverseHandler(ErpDbContext db) =>
        new(
            new SupplierPaymentRepository(db),
            new AccountsPayableRepository(db),
            new CashSessionRepository(db, new FixedCurrentCompany(_companyId)),
            new UnitOfWork(db),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentUser(_createdBy)
        );

    /// <summary>SUPPLIER-PAYMENTS-REVERSE-16 — siembra tanto la regla de confirmación como la de
    /// reverso, más un único período compartido (mismo criterio que
    /// CollectionPostingIntegrationTests.SeedAppliedAndReversedRulesAndPeriodAsync).</summary>
    private async Task SeedConfirmedAndReversedRulesAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
    {
        var confirmedRule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Payables",
            "SupplierPaymentConfirmed",
            null,
            null,
            null,
            _createdBy
        );
        confirmedRule.AddLine(_payablesAccountId, AccountNature.Debit, PostingAmountKind.GrandTotal);

        var reversedRule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Payables",
            "SupplierPaymentReversed",
            null,
            null,
            null,
            _createdBy
        );
        reversedRule.AddLine(_payablesAccountId, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(
                entryDate.Year,
                entryDate.Month,
                DateTime.DaysInMonth(entryDate.Year, entryDate.Month)
            ),
            _createdBy
        );

        db.PostingRules.AddRange(confirmedRule, reversedRule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 1 medio / 1 cuota
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Pago_1_medio_1_cuota_confirma_paga_la_cuota_por_completo_y_postea_balanceado()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var cmd = new RegisterSupplierPaymentCommand(
            _supplierId,
            paymentDate,
            300m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
            new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
        );

        var result = await BuildHandler(db).Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        var dto = result.Value!;
        dto.SystemNumber.Should().NotBeNullOrWhiteSpace();
        dto.DisplayNumber.Should().Be(dto.SystemNumber, "sin receipt_number, DisplayNumber = SystemNumber");
        dto.ReceiptNumber.Should().BeNull();

        await using var verifyDb = CreateContext();
        var payment = await verifyDb
            .SupplierPayments.Include(x => x.MethodLines)
            .Include(x => x.ApplicationLines)
            .Include(x => x.AllocationLines)
            .FirstAsync(x => x.Id == dto.Id);
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed);
        payment.MethodLines.Should().ContainSingle();
        payment.ApplicationLines.Should().ContainSingle();
        payment.AllocationLines.Should().ContainSingle();

        var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _purchaseInstallmentId
        );
        installment.PaidAmount.Should().Be(300m);
        installment.OutstandingAmount.Should().Be(0m);
        installment.Status.Should().Be(AccountsPayableStatus.Paid);

        var payable = await verifyDb.AccountsPayables.FirstAsync(x => x.Id == _purchasePayableId);
        payable.PaidAmount.Should().Be(300m);
        payable.OutstandingAmount.Should().Be(0m);
        payable.Status.Should().Be(AccountsPayableStatus.Paid);

        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == dto.Id);
        entry.Should().NotBeNull();
        entry!.SourceModule.Should().Be("Payables");
        entry.SourceEventType.Should().Be("SupplierPaymentConfirmed");
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(2, "1 débito CxP + 1 crédito por el único medio de pago");
        entry.Lines.Sum(l => l.Debit).Should().Be(300m);
        entry.Lines.Sum(l => l.Credit).Should().Be(300m);
        entry.Lines.Single(l => l.Credit > 0).AccountId.Should().Be(_cashAccountId);
        entry.Lines.Single(l => l.Debit > 0).AccountId.Should().Be(_payablesAccountId);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 2 medios / 1 cuota
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Pago_2_medios_1_cuota_genera_2_creditos_y_1_debito_balanceados()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var cmd = new RegisterSupplierPaymentCommand(
            _supplierId,
            paymentDate,
            300m,
            "CHK-0001",
            new[]
            {
                new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 100m),
                new SupplierPaymentMethodLineRequest(_transferMethodId, _companyBankAccountId, null, 200m, "OP-E2E", TransactionDate: paymentDate),
            },
            new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
            new[]
            {
                new SupplierPaymentAllocationLineRequest(0, 0, 100m),
                new SupplierPaymentAllocationLineRequest(1, 0, 200m),
            }
        );

        var result = await BuildHandler(db).Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        var dto = result.Value!;
        dto.ReceiptNumber.Should().Be("CHK-0001");
        dto.DisplayNumber.Should().Be("CHK-0001", "con receipt_number informado, DisplayNumber lo usa");

        await using var verifyDb = CreateContext();
        var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _purchaseInstallmentId
        );
        installment.Status.Should().Be(AccountsPayableStatus.Paid);

        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceEventId == dto.Id);
        entry.Lines.Should().HaveCount(3, "1 débito CxP + 2 créditos, uno por cada medio");
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Sum(l => l.Debit).Should().Be(300m);
        entry.Lines.Count(l => l.Credit > 0).Should().Be(2);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 1 medio / 2 cuotas (Compras + Gastos) — postea por medio, no por cuota
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Pago_1_medio_2_cuotas_de_compras_y_gastos_actualiza_ambas_CxP_y_postea_1_solo_credito()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var cmd = new RegisterSupplierPaymentCommand(
            _supplierId,
            paymentDate,
            500m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(_transferMethodId, _companyBankAccountId, null, 500m, "OP-E2E", TransactionDate: paymentDate) },
            new[]
            {
                new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m),
                new SupplierPaymentApplicationLineRequest(_expenseInstallmentId, 200m),
            },
            new[]
            {
                new SupplierPaymentAllocationLineRequest(0, 0, 300m),
                new SupplierPaymentAllocationLineRequest(0, 1, 200m),
            }
        );

        var result = await BuildHandler(db).Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        var dto = result.Value!;

        await using var verifyDb = CreateContext();
        var purchasePayable = await verifyDb.AccountsPayables.FirstAsync(x => x.Id == _purchasePayableId);
        var expensePayable = await verifyDb.AccountsPayables.FirstAsync(x => x.Id == _expensePayableId);
        purchasePayable.Status.Should().Be(AccountsPayableStatus.Paid);
        purchasePayable.OutstandingAmount.Should().Be(0m);
        expensePayable.Status.Should().Be(AccountsPayableStatus.Paid);
        expensePayable.OutstandingAmount.Should().Be(0m);

        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceEventId == dto.Id);
        entry.Lines.Should()
            .HaveCount(
                2,
                "un solo medio de pago => 1 débito + 1 crédito, sin importar que hayan sido 2 cuotas"
            );
        entry.Lines.Sum(l => l.Debit).Should().Be(500m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 2 medios / 2 cuotas, matriz cruzada — PartiallyPaid
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Pago_2_medios_2_cuotas_parcial_deja_ambas_cuotas_PartiallyPaid()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var cmd = new RegisterSupplierPaymentCommand(
            _supplierId,
            paymentDate,
            300m,
            null,
            new[]
            {
                new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 150m),
                new SupplierPaymentMethodLineRequest(_transferMethodId, _companyBankAccountId, null, 150m, "OP-E2E", TransactionDate: paymentDate),
            },
            new[]
            {
                new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 150m), // de 300
                new SupplierPaymentApplicationLineRequest(_expenseInstallmentId, 150m), // de 200
            },
            new[]
            {
                new SupplierPaymentAllocationLineRequest(0, 0, 100m),
                new SupplierPaymentAllocationLineRequest(0, 1, 50m),
                new SupplierPaymentAllocationLineRequest(1, 0, 50m),
                new SupplierPaymentAllocationLineRequest(1, 1, 100m),
            }
        );

        var result = await BuildHandler(db).Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        var dto = result.Value!;

        await using var verifyDb = CreateContext();
        var purchaseInstallment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _purchaseInstallmentId
        );
        var expenseInstallment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _expenseInstallmentId
        );
        purchaseInstallment.Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
        purchaseInstallment.OutstandingAmount.Should().Be(150m);
        expenseInstallment.Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
        expenseInstallment.OutstandingAmount.Should().Be(50m);

        var purchasePayable = await verifyDb.AccountsPayables.FirstAsync(x => x.Id == _purchasePayableId);
        purchasePayable.Status.Should().Be(AccountsPayableStatus.PartiallyPaid);

        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceEventId == dto.Id);
        entry.Lines.Should().HaveCount(3, "1 débito CxP + 2 créditos, uno por cada medio");
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
    }

    // ══════════════════════════════════════════════════════════════════════
    // Rollback total si falla el posting (sin PostingRule sembrada)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Si_falla_el_posting_no_queda_SupplierPayment_ni_saldos_alterados()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        // Deliberadamente sin sembrar la PostingRule "Payables"/"SupplierPaymentConfirmed" —
        // fuerza RULE_NOT_FOUND dentro del Posting Engine.

        var cmd = new RegisterSupplierPaymentCommand(
            _supplierId,
            paymentDate,
            300m,
            null,
            new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
            new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
        );

        var result = await BuildHandler(db).Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse("un pago a proveedor nunca queda confirmado sin asiento");

        await using var verifyDb = CreateContext();
        var supplierPaymentCount = await verifyDb.SupplierPayments.CountAsync(x =>
            x.SupplierId == _supplierId
        );
        var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _purchaseInstallmentId
        );
        var journalEntryCount = await verifyDb.JournalEntries.CountAsync(x =>
            x.SourceModule == "Payables"
        );

        supplierPaymentCount.Should().Be(0, "el registro completo debe revertirse, no solo el asiento");
        installment.PaidAmount.Should().Be(0m, "la cuota no debe quedar parcialmente pagada");
        installment.OutstandingAmount.Should().Be(300m);
        journalEntryCount.Should().Be(0);
    }

    // ══════════════════════════════════════════════════════════════════════
    // SUPPLIER-PAYMENTS-REVERSE-16 — reversa de pagos confirmados
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reversar_pago_total_vuelve_la_cuota_de_Paid_a_Pending_y_postea_asiento_inverso_balanceado()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedConfirmedAndReversedRulesAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    300m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext();
        var reverseResult = await BuildReverseHandler(dbReverse)
            .Handle(
                new ReverseSupplierPaymentCommand(paymentId, "Error de digitación"),
                CancellationToken.None
            );

        reverseResult.IsSuccess.Should().BeTrue(because: reverseResult.Error);
        reverseResult.Value!.Status.Should().Be("Reversed");

        await using var verifyDb = CreateContext();
        var payment = await verifyDb.SupplierPayments.FirstAsync(x => x.Id == paymentId);
        payment.Status.Should().Be(SupplierPaymentStatus.Reversed);
        payment.ReverseReason.Should().Be("Error de digitación");
        payment.ReversedAtUtc.Should().NotBeNull();
        payment.ReversedBy.Should().NotBeNull();

        var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _purchaseInstallmentId
        );
        installment.PaidAmount.Should().Be(0m);
        installment.OutstandingAmount.Should().Be(300m);
        installment.Status.Should().Be(AccountsPayableStatus.Pending);

        var payable = await verifyDb.AccountsPayables.FirstAsync(x => x.Id == _purchasePayableId);
        payable.Status.Should().Be(AccountsPayableStatus.Pending);

        var reversedEntry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x =>
                x.SourceEventId == paymentId && x.SourceEventType == "SupplierPaymentReversed"
            );
        reversedEntry.Should().NotBeNull();
        reversedEntry!.SourceModule.Should().Be("Payables");
        reversedEntry.Status.Should().Be(JournalEntryStatus.Posted);
        reversedEntry.Lines.Should().HaveCount(2, "1 crédito CxP + 1 débito por el único medio original");
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(reversedEntry.Lines.Sum(l => l.Credit));
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(300m);

        var confirmedEntry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x =>
            x.SourceEventId == paymentId && x.SourceEventType == "SupplierPaymentConfirmed"
        );
        confirmedEntry.Should().NotBeNull(because: "el asiento original de confirmación no debe desaparecer");
    }

    [Fact]
    public async Task Reversar_pago_parcial_deja_saldos_correctos_en_la_cuota_y_la_cabecera()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedConfirmedAndReversedRulesAndPeriodAsync(db, paymentDate);

        // Pago parcial: 100 de los 300 de la cuota.
        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    100m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 100m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 100m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext();
        var reverseResult = await BuildReverseHandler(dbReverse)
            .Handle(
                new ReverseSupplierPaymentCommand(paymentId, "Cheque rechazado"),
                CancellationToken.None
            );

        reverseResult.IsSuccess.Should().BeTrue(because: reverseResult.Error);

        await using var verifyDb = CreateContext();
        var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _purchaseInstallmentId
        );
        installment.PaidAmount.Should().Be(0m);
        installment.OutstandingAmount.Should().Be(300m);
        installment.Status.Should().Be(AccountsPayableStatus.Pending);
    }

    [Fact]
    public async Task Reversar_pago_con_2_medios_genera_asiento_inverso_con_2_debitos_banco_caja()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedConfirmedAndReversedRulesAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    300m,
                    null,
                    new[]
                    {
                        new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 100m),
                        new SupplierPaymentMethodLineRequest(_transferMethodId, _companyBankAccountId, null, 200m, "OP-E2E", TransactionDate: paymentDate),
                    },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                    new[]
                    {
                        new SupplierPaymentAllocationLineRequest(0, 0, 100m),
                        new SupplierPaymentAllocationLineRequest(1, 0, 200m),
                    }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext();
        var reverseResult = await BuildReverseHandler(dbReverse)
            .Handle(
                new ReverseSupplierPaymentCommand(paymentId, "Duplicado"),
                CancellationToken.None
            );

        reverseResult.IsSuccess.Should().BeTrue(because: reverseResult.Error);

        await using var verifyDb = CreateContext();
        var reversedEntry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x =>
                x.SourceEventId == paymentId && x.SourceEventType == "SupplierPaymentReversed"
            );
        reversedEntry.Lines.Should()
            .HaveCount(3, "1 crédito CxP + 2 débitos, uno por cada medio original (banco y caja)");
        reversedEntry.Lines.Count(l => l.Debit > 0).Should().Be(2);
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(reversedEntry.Lines.Sum(l => l.Credit));
        reversedEntry.Lines.Sum(l => l.Debit).Should().Be(300m);
    }

    [Fact]
    public async Task Bloquea_doble_reversa_end_to_end()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedConfirmedAndReversedRulesAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    300m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
                ),
                CancellationToken.None
            );
        var paymentId = registerResult.Value!.Id;

        var (dbFirstReverse, _) = BuildWiredContext();
        var firstReverse = await BuildReverseHandler(dbFirstReverse)
            .Handle(new ReverseSupplierPaymentCommand(paymentId, "Motivo 1"), CancellationToken.None);
        firstReverse.IsSuccess.Should().BeTrue(because: firstReverse.Error);

        var (dbSecondReverse, _) = BuildWiredContext();
        var secondReverse = await BuildReverseHandler(dbSecondReverse)
            .Handle(new ReverseSupplierPaymentCommand(paymentId, "Motivo 2"), CancellationToken.None);

        secondReverse.IsSuccess.Should().BeFalse("un pago ya Reversed no puede reversarse otra vez");

        await using var verifyDb = CreateContext();
        var reversedEntryCount = await verifyDb.JournalEntries.CountAsync(x =>
            x.SourceEventId == paymentId && x.SourceEventType == "SupplierPaymentReversed"
        );
        reversedEntryCount.Should().Be(1, "la segunda reversa nunca debe generar un segundo asiento inverso");
    }

    [Fact]
    public async Task Si_falla_el_posting_inverso_el_pago_sigue_Confirmed_y_los_saldos_no_cambian()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        // Solo siembra la regla de confirmación — la de reverso queda deliberadamente ausente
        // para forzar RULE_NOT_FOUND en el Posting Engine al reversar.
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    300m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext();
        var reverseResult = await BuildReverseHandler(dbReverse)
            .Handle(
                new ReverseSupplierPaymentCommand(paymentId, "Error de digitación"),
                CancellationToken.None
            );

        reverseResult.IsSuccess.Should().BeFalse("un reverso nunca debe quedar confirmado sin asiento inverso");

        await using var verifyDb = CreateContext();
        var payment = await verifyDb.SupplierPayments.FirstAsync(x => x.Id == paymentId);
        payment.Status.Should().Be(SupplierPaymentStatus.Confirmed, "el reverso fallido no debe mutar el estado");

        var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
            x.Id == _purchaseInstallmentId
        );
        installment.PaidAmount.Should().Be(300m, "el reverso fallido no debe liberar saldo");
        installment.OutstandingAmount.Should().Be(0m);

        var reversedEntryCount = await verifyDb.JournalEntries.CountAsync(x =>
            x.SourceEventType == "SupplierPaymentReversed"
        );
        reversedEntryCount.Should().Be(0);
    }

    // ══════════════════════════════════════════════════════════════════════
    // SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — reproducción/regresión contra
    // Postgres real: el detalle debe resolver documentNumber/installmentNumber/dueDate de la cuota
    // aplicada aunque la CxP/cuota ya haya quedado Paid (o el pago Reversed) — nunca depender de
    // que la cuota siga "pendiente".
    // ══════════════════════════════════════════════════════════════════════

    private GetSupplierPaymentByIdHandler BuildGetByIdHandler(ErpDbContext db) =>
        new(
            new SupplierPaymentRepository(db),
            new AccountsPayableRepository(db),
            new FixedCurrentTenant(_tenantId)
        );

    [Fact]
    public async Task Detalle_de_pago_confirmado_total_muestra_documentNumber_installmentNumber_y_dueDate_aunque_la_cuota_quede_Paid()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    300m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        // La cuota ya quedó Paid tras el pago total — confirma que el detalle no depende de
        // "pendiente" para resolver.
        await using (var verifyDb = CreateContext())
        {
            var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
                x.Id == _purchaseInstallmentId
            );
            installment.Status.Should().Be(AccountsPayableStatus.Paid);
        }

        await using var readDb = CreateContext();
        var detailResult = await BuildGetByIdHandler(readDb)
            .Handle(new GetSupplierPaymentByIdQuery(paymentId), CancellationToken.None);

        detailResult.IsSuccess.Should().BeTrue(because: detailResult.Error);
        var line = detailResult.Value!.ApplicationLines.Should().ContainSingle().Subject;
        line.AccountsPayableInstallmentId.Should().Be(_purchaseInstallmentId);
        line.DocumentNumber.Should().Be("001-001-000000001");
        line.InstallmentNumber.Should().Be(1);
        line.DueDate.Should().Be(new DateOnly(2026, 9, 1));
        line.IssueDate.Should().Be(new DateOnly(2026, 8, 1));
        line.OriginType.Should().Be("PurchaseInvoice");
    }

    [Fact]
    public async Task Detalle_de_pago_parcial_muestra_datos_de_la_cuota_aunque_quede_PartiallyPaid()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    100m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 100m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 100m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 100m) }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        await using (var verifyDb = CreateContext())
        {
            var installment = await verifyDb.AccountsPayableInstallments.FirstAsync(x =>
                x.Id == _purchaseInstallmentId
            );
            installment.Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
        }

        await using var readDb = CreateContext();
        var detailResult = await BuildGetByIdHandler(readDb)
            .Handle(new GetSupplierPaymentByIdQuery(paymentId), CancellationToken.None);

        detailResult.IsSuccess.Should().BeTrue(because: detailResult.Error);
        var line = detailResult.Value!.ApplicationLines.Should().ContainSingle().Subject;
        line.DocumentNumber.Should().Be("001-001-000000001");
        line.InstallmentNumber.Should().Be(1);
        line.DueDate.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public async Task Detalle_de_pago_reversado_sigue_mostrando_datos_de_la_cuota()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedConfirmedAndReversedRulesAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    300m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        var (dbReverse, _) = BuildWiredContext();
        var reverseResult = await BuildReverseHandler(dbReverse)
            .Handle(new ReverseSupplierPaymentCommand(paymentId, "Duplicado"), CancellationToken.None);
        reverseResult.IsSuccess.Should().BeTrue(because: reverseResult.Error);

        await using var readDb = CreateContext();
        var detailResult = await BuildGetByIdHandler(readDb)
            .Handle(new GetSupplierPaymentByIdQuery(paymentId), CancellationToken.None);

        detailResult.IsSuccess.Should().BeTrue(because: detailResult.Error);
        detailResult.Value!.Status.Should().Be("Reversed");
        var line = detailResult.Value.ApplicationLines.Should().ContainSingle().Subject;
        line.DocumentNumber.Should().Be("001-001-000000001");
        line.InstallmentNumber.Should().Be(1);
    }

    [Fact]
    public async Task Otra_empresa_no_puede_resolver_la_cuota_de_otra_company_en_el_detalle()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var registerResult = await BuildHandler(db)
            .Handle(
                new RegisterSupplierPaymentCommand(
                    _supplierId,
                    paymentDate,
                    300m,
                    null,
                    new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
                    new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                    new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
                ),
                CancellationToken.None
            );
        registerResult.IsSuccess.Should().BeTrue(because: registerResult.Error);
        var paymentId = registerResult.Value!.Id;

        // Empresa B (mismo tenant, otra compañía activa): el pago mismo ya no es visible por
        // GetByIdAsync (solo filtra por TenantId, IGUAL COMPORTAMIENTO PREVIO — el detalle es
        // company-scoped por el filtro global de EF sobre AccountsPayable/SupplierPayment), así
        // que confirmamos primero que el pago no resuelve para otra empresa.
        var otherCompanyId = Guid.NewGuid();
        await using var otherCompanyDb = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(otherCompanyId)
        );
        var detailResult = await BuildGetByIdHandler(otherCompanyDb)
            .Handle(new GetSupplierPaymentByIdQuery(paymentId), CancellationToken.None);

        detailResult.IsSuccess.Should().BeFalse(
            "el pago pertenece a la Empresa A — el filtro global de EF sobre SupplierPayment (ICompanyOperationalEntity) no debe exponerlo a otra empresa"
        );
    }

    /// <summary>
    /// El caso "la cuota ya no se puede resolver" no es reproducible contra Postgres real con un
    /// pago legítimo: <c>accounts_payable_installments</c> tiene FK entrante desde
    /// <c>supplier_payment_applications</c> — Postgres bloquea con 23503 cualquier intento de
    /// borrar la CxP/cuota que un pago referencia (evidencia de que el escenario es, en efecto,
    /// excepcional/inalcanzable por el flujo normal). El fallback fail-safe (campos en <c>null</c>
    /// sin romper el detalle) se prueba a nivel de handler con un repositorio mockeado en
    /// <c>GetSupplierPaymentUseCasesTests.GetById_si_no_resuelve_la_cuota_no_rompe_el_detalle_y_deja_los_campos_en_null</c>.
    /// </summary>
    [Fact]
    public void Fallback_de_cuota_no_resoluble_esta_cubierto_por_GetSupplierPaymentUseCasesTests()
    {
        // Ver comentario de la clase — ningún assert adicional aquí, es solo el puntero.
    }

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Inner!.Publish(notification, cancellationToken);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class FixedCurrentBranch(Guid branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId;
        public bool IsAuthenticated => true;
        public bool HasBranchContext => branchId != Guid.Empty;
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

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — Postgres real
    // ══════════════════════════════════════════════════════════════════════

    private async Task<CashSession> LoadSessionAsync()
    {
        await using var verifyDb = CreateContext();
        return await verifyDb.Set<CashSession>().AsNoTracking().Include(x => x.Movements).FirstAsync(x => x.Id == _cashSessionId);
    }

    [Fact]
    public async Task Efectivo_persiste_egreso_de_caja_vinculado_baja_el_esperado_y_postea_un_solo_asiento()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db).Handle(
            new RegisterSupplierPaymentCommand(
                _supplierId,
                paymentDate,
                300m,
                null,
                new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 300m) },
                new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 300m) },
                new[] { new SupplierPaymentAllocationLineRequest(0, 0, 300m) }
            ),
            CancellationToken.None
        );
        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verifyDb = CreateContext();
        var line = await verifyDb.Set<SupplierPaymentMethodLine>().AsNoTracking().SingleAsync(x => x.SupplierPaymentId == result.Value!.Id);
        line.CashSessionId.Should().Be(_cashSessionId);
        line.CashMovementId.Should().NotBeNull();
        line.TransactionDate.Should().BeNull();

        var session = await LoadSessionAsync();
        var movement = session.Movements.Single(x => x.Id == line.CashMovementId);
        movement.MovementType.Should().Be(CashMovementType.SupplierPayment);
        movement.Amount.Should().Be(300m);
        movement.ReferenceType.Should().Be(CashReferenceType.SupplierPayment);
        movement.ReferenceId.Should().Be(result.Value!.Id);
        movement.ReferenceNumber.Should().Be(result.Value.SystemNumber);
        session.CurrentBalance.Should().Be(OpeningCash - 300m);

        // Sin doble contabilización: un único asiento (SupplierPayment) y ninguno con origen en Caja.
        var entries = await verifyDb.JournalEntries.AsNoTracking().ToListAsync();
        entries.Should().ContainSingle();
        entries[0].SourceEventId.Should().Be(result.Value.Id);
        entries[0].SourceModule.Should().Be("Payables");
        entries.Should().NotContain(e => e.SourceEventId == movement.Id);
    }

    [Fact]
    public async Task Reversa_de_efectivo_persiste_ingreso_compensatorio_conserva_el_original_y_restaura_el_esperado()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var (db, _) = BuildWiredContext();
        await SeedConfirmedAndReversedRulesAndPeriodAsync(db, paymentDate);
        var registerResult = await BuildHandler(db).Handle(
            new RegisterSupplierPaymentCommand(
                _supplierId,
                paymentDate,
                100m,
                null,
                new[]
                {
                    new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 40m),
                    new SupplierPaymentMethodLineRequest(_transferMethodId, _companyBankAccountId, null, 60m, "OP-MIX", TransactionDate: paymentDate),
                },
                new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 100m) },
                new[]
                {
                    new SupplierPaymentAllocationLineRequest(0, 0, 40m),
                    new SupplierPaymentAllocationLineRequest(1, 0, 60m),
                }
            ),
            CancellationToken.None
        );
        registerResult.IsSuccess.Should().BeTrue(registerResult.Error);
        (await LoadSessionAsync()).CurrentBalance.Should().Be(OpeningCash - 40m, "solo la fuente de efectivo mueve el cajón");

        var (reverseDb, _) = BuildWiredContext();
        var reverseResult = await BuildReverseHandler(reverseDb).Handle(
            new ReverseSupplierPaymentCommand(registerResult.Value!.Id, "Pago duplicado"),
            CancellationToken.None
        );
        reverseResult.IsSuccess.Should().BeTrue(reverseResult.Error);

        var session = await LoadSessionAsync();
        var paymentMovements = session.Movements
            .Where(x => x.ReferenceType == CashReferenceType.SupplierPayment && x.ReferenceId == registerResult.Value.Id)
            .ToList();
        paymentMovements.Should().HaveCount(2, "egreso original intacto + ingreso compensatorio");
        paymentMovements.Should().ContainSingle(x => x.MovementType == CashMovementType.SupplierPayment && x.Amount == 40m);
        paymentMovements.Should().ContainSingle(x => x.MovementType == CashMovementType.SupplierPaymentReversal && x.Amount == 40m);
        session.CurrentBalance.Should().Be(OpeningCash);

        await using var verifyDb = CreateContext();
        var entries = await verifyDb.JournalEntries.AsNoTracking().ToListAsync();
        entries.Should().HaveCount(2, "asiento de confirmación + asiento inverso, ninguno de Caja");
        entries.Should().OnlyContain(e => e.SourceEventId == registerResult.Value.Id);
    }

    [Fact]
    public async Task Transferencia_persiste_cuenta_fecha_referencia_y_monto_y_no_mueve_caja()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        var bankDate = new DateOnly(2026, 8, 26);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db).Handle(
            new RegisterSupplierPaymentCommand(
                _supplierId,
                paymentDate,
                200m,
                null,
                new[]
                {
                    new SupplierPaymentMethodLineRequest(
                        _transferMethodId,
                        _companyBankAccountId,
                        null,
                        200m,
                        ReferenceNumber: "000555111",
                        TransactionDate: bankDate
                    ),
                },
                new[] { new SupplierPaymentApplicationLineRequest(_expenseInstallmentId, 200m) },
                new[] { new SupplierPaymentAllocationLineRequest(0, 0, 200m) }
            ),
            CancellationToken.None
        );
        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verifyDb = CreateContext();
        var payment = await verifyDb.SupplierPayments.AsNoTracking().Include(x => x.MethodLines).SingleAsync(x => x.Id == result.Value!.Id);
        var line = payment.MethodLines.Single();
        // Match futuro: Cuenta + Fecha + Referencia + Monto (+ SupplierId/SystemNumber en cabecera).
        line.CompanyBankAccountId.Should().Be(_companyBankAccountId);
        line.TransactionDate.Should().Be(bankDate);
        line.ReferenceNumber.Should().Be("000555111");
        line.Amount.Should().Be(200m);
        line.PaymentMethodId.Should().Be(_transferMethodId);
        payment.SupplierId.Should().Be(_supplierId);
        payment.SystemNumber.Should().NotBeNullOrWhiteSpace();
        line.CashSessionId.Should().BeNull();
        line.CashMovementId.Should().BeNull();

        var session = await LoadSessionAsync();
        session.Movements.Should().ContainSingle("solo la apertura — una transferencia nunca toca el cajón");
        session.CurrentBalance.Should().Be(OpeningCash);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-HARDENING-02A-CLOSE — sin sobregiro, Postgres real
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Deja la sesión con exactamente <paramref name="available"/> de efectivo esperado
    /// (retiro previo de la diferencia) — escenario "Caja disponible: $80".</summary>
    private async Task LeaveCashAvailableAsync(decimal available)
    {
        await using var db = CreateContext();
        var session = await db.Set<CashSession>().Include(x => x.Movements).FirstAsync(x => x.Id == _cashSessionId);
        session.RecordMovement(CashMovementType.Withdrawal, session.CurrentBalance - available, "Depósito previo", _createdBy);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Efectivo_mayor_al_disponible_se_rechaza_sin_persistir_pago_movimiento_ni_asiento()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        await LeaveCashAvailableAsync(80m);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db).Handle(
            new RegisterSupplierPaymentCommand(
                _supplierId,
                paymentDate,
                120m,
                null,
                new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 120m) },
                new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 120m) },
                new[] { new SupplierPaymentAllocationLineRequest(0, 0, 120m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La caja seleccionada dispone de $80.00 y se intenta registrar un pago de $120.00.");

        await using var verifyDb = CreateContext();
        (await verifyDb.SupplierPayments.AsNoTracking().AnyAsync()).Should().BeFalse();
        (await verifyDb.JournalEntries.AsNoTracking().AnyAsync()).Should().BeFalse();
        var installment = await verifyDb.AccountsPayableInstallments.AsNoTracking().FirstAsync(x => x.Id == _purchaseInstallmentId);
        installment.PaidAmount.Should().Be(0m);
        var session = await LoadSessionAsync();
        session.Movements.Should().NotContain(x => x.MovementType == CashMovementType.SupplierPayment);
        session.CurrentBalance.Should().Be(80m);
    }

    [Fact]
    public async Task Pago_mixto_de_200_usa_80_de_caja_y_120_de_banco_con_un_solo_asiento()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        await LeaveCashAvailableAsync(80m);
        var (db, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(db, paymentDate);

        var result = await BuildHandler(db).Handle(
            new RegisterSupplierPaymentCommand(
                _supplierId,
                paymentDate,
                200m,
                null,
                new[]
                {
                    new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 80m),
                    new SupplierPaymentMethodLineRequest(_transferMethodId, _companyBankAccountId, null, 120m, "OP-200", TransactionDate: paymentDate),
                },
                new[] { new SupplierPaymentApplicationLineRequest(_purchaseInstallmentId, 200m) },
                new[]
                {
                    new SupplierPaymentAllocationLineRequest(0, 0, 80m),
                    new SupplierPaymentAllocationLineRequest(1, 0, 120m),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var session = await LoadSessionAsync();
        session.CurrentBalance.Should().Be(0m, "la caja entrega todo su disponible, nunca queda negativa");
        session.Movements.Should().ContainSingle(x => x.MovementType == CashMovementType.SupplierPayment && x.Amount == 80m);

        await using var verifyDb = CreateContext();
        var entries = await verifyDb.JournalEntries.AsNoTracking().Include(x => x.Lines).ToListAsync();
        entries.Should().ContainSingle("SupplierPayment es la única fuente del asiento");
        entries[0].Lines.Sum(l => l.Debit).Should().Be(200m);
        entries[0].Lines.Single(l => l.AccountId == _cashAccountId).Credit.Should().Be(80m);
        entries[0].Lines.Single(l => l.AccountId == _bankAccountId).Credit.Should().Be(120m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-02A-FINAL — concurrencia real sobre la misma CashSession
    // ══════════════════════════════════════════════════════════════════════

    private RegisterSupplierPaymentCommand CashPaymentCommand(Guid installmentId, decimal amount, DateOnly paymentDate) =>
        new(
            _supplierId,
            paymentDate,
            amount,
            null,
            new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, amount) },
            new[] { new SupplierPaymentApplicationLineRequest(installmentId, amount) },
            new[] { new SupplierPaymentAllocationLineRequest(0, 0, amount) }
        );

    /// <summary>
    /// Saldo 100, dos pagos simultáneos de 70 contra la misma caja. Para garantizar que ambos
    /// compiten de verdad (no por azar de scheduling), una conexión externa retiene FOR UPDATE sobre
    /// la sesión hasta que PostgreSQL reporta a AMBOS handlers esperando ese lock; al liberarlo, el
    /// FOR UPDATE del handler los serializa: uno confirma y el otro, al recargar la sesión bajo el
    /// lock, ve 30 disponibles y recibe la validación normal de saldo — nunca un deadlock genérico.
    /// </summary>
    [Fact]
    public async Task Dos_pagos_concurrentes_de_70_sobre_caja_de_100_se_serializan_y_solo_uno_confirma()
    {
        var paymentDate = new DateOnly(2026, 8, 28);
        await LeaveCashAvailableAsync(100m);
        var (seedDb, _) = BuildWiredContext();
        await SeedPostingRuleAndPeriodAsync(seedDb, paymentDate);

        await using var blocker = new NpgsqlConnection(_postgres.GetConnectionString());
        await blocker.OpenAsync();
        await using var blockerTx = await blocker.BeginTransactionAsync();
        await using (var lockCmd = new NpgsqlCommand("SELECT 1 FROM cash_sessions WHERE id = @id FOR UPDATE", blocker, blockerTx))
        {
            lockCmd.Parameters.AddWithValue("id", _cashSessionId);
            await lockCmd.ExecuteNonQueryAsync();
        }

        var (dbA, _) = BuildWiredContext();
        var (dbB, _) = BuildWiredContext();
        var paymentA = Task.Run(() => BuildHandler(dbA).Handle(CashPaymentCommand(_purchaseInstallmentId, 70m, paymentDate), CancellationToken.None));
        var paymentB = Task.Run(() => BuildHandler(dbB).Handle(CashPaymentCommand(_expenseInstallmentId, 70m, paymentDate), CancellationToken.None));

        // Espera observable (no un sleep ciego): ambos backends bloqueados en el lock de fila.
        var deadline = DateTime.UtcNow.AddSeconds(20);
        long waiting = 0;
        while (DateTime.UtcNow < deadline)
        {
            await using var probe = new NpgsqlConnection(_postgres.GetConnectionString());
            await probe.OpenAsync();
            await using var countCmd = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'",
                probe
            );
            waiting = (long)(await countCmd.ExecuteScalarAsync())!;
            if (waiting >= 2)
                break;
            await Task.Delay(50);
        }
        waiting.Should().BeGreaterThanOrEqualTo(2, "ambos pagos deben estar compitiendo por la misma CashSession");

        await blockerTx.CommitAsync();
        var results = await Task.WhenAll(paymentA, paymentB);

        results.Count(r => r.IsSuccess).Should().Be(1, "solo un pago puede consumir el efectivo");
        var rejected = results.Single(r => !r.IsSuccess);
        rejected.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        rejected.Error.Should().Be("La caja seleccionada dispone de $30.00 y se intenta registrar un pago de $70.00.");
        var confirmed = results.Single(r => r.IsSuccess).Value!;

        await using var verifyDb = CreateContext();
        var payments = await verifyDb.SupplierPayments.AsNoTracking().ToListAsync();
        payments.Should().ContainSingle();
        payments[0].Id.Should().Be(confirmed.Id);
        payments[0].Status.Should().Be(SupplierPaymentStatus.Confirmed);

        var session = await LoadSessionAsync();
        session.CurrentBalance.Should().Be(30m, "nunca negativo: 100 − 70");
        session.Movements.Where(x => x.MovementType == CashMovementType.SupplierPayment).Should().ContainSingle()
            .Which.ReferenceId.Should().Be(confirmed.Id);

        var entries = await verifyDb.JournalEntries.AsNoTracking().ToListAsync();
        entries.Should().ContainSingle().Which.SourceEventId.Should().Be(confirmed.Id);

        // Sin datos parciales del pago rechazado: su cuota sigue intacta.
        var installments = await verifyDb.AccountsPayableInstallments.AsNoTracking()
            .Where(x => x.Id == _purchaseInstallmentId || x.Id == _expenseInstallmentId)
            .ToListAsync();
        installments.Sum(x => x.PaidAmount).Should().Be(70m);
        installments.Should().ContainSingle(x => x.PaidAmount == 0m);
    }
}
