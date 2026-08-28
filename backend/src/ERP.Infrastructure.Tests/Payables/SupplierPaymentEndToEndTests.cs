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
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
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
    private Guid _cashDestinationId;
    private Guid _bankDestinationId;
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
        var cashMethod = PaymentMethod.Create(_tenantId, "EFEC", "Efectivo", false, false, 1, _createdBy);
        var transferMethod = PaymentMethod.Create(
            _tenantId,
            "TRANS",
            "Transferencia",
            true,
            false,
            2,
            _createdBy
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
        db.CashRegisters.Add(cashRegisterEntity);
        await db.SaveChangesAsync();

        var cashDestination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "CAJA-01",
            "Caja Principal",
            FinancialDestinationTypeCode.CashRegister,
            cashAccount.Id,
            "USD",
            _createdBy,
            cashRegisterId: cashRegisterEntity.Id
        );
        var bankDestination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "BANCO-01",
            "Banco Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            bankAccount.Id,
            "USD",
            _createdBy,
            bankInstitutionCode: "PICHINCHA",
            bankAccountIdentifierNormalized: "2200123456"
        );
        db.CompanyFinancialDestinations.AddRange(cashDestination, bankDestination);
        await db.SaveChangesAsync();

        _cashMethodId = cashMethod.Id;
        _transferMethodId = transferMethod.Id;
        _cashDestinationId = cashDestination.Id;
        _bankDestinationId = bankDestination.Id;
        _cashAccountId = cashAccount.Id;
        _bankAccountId = bankAccount.Id;
        _payablesAccountId = payablesAccount.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
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
            .Options;
        var db = new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            deferred,
            new FixedCurrentCompany(_companyId)
        );

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(_companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<
            ICompanyFinancialDestinationRepository,
            CompanyFinancialDestinationRepository
        >();
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
            new CompanyFinancialDestinationRepository(db, new FixedCurrentCompany(_companyId)),
            new UnitOfWork(db),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(_companyId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_createdBy)
        );

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
            new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, _cashDestinationId, 300m) },
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
                new SupplierPaymentMethodLineRequest(_cashMethodId, _cashDestinationId, 100m),
                new SupplierPaymentMethodLineRequest(_transferMethodId, _bankDestinationId, 200m),
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
            new[] { new SupplierPaymentMethodLineRequest(_transferMethodId, _bankDestinationId, 500m) },
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
                new SupplierPaymentMethodLineRequest(_cashMethodId, _cashDestinationId, 150m),
                new SupplierPaymentMethodLineRequest(_transferMethodId, _bankDestinationId, 150m),
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
            new[] { new SupplierPaymentMethodLineRequest(_cashMethodId, _cashDestinationId, 300m) },
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
}
