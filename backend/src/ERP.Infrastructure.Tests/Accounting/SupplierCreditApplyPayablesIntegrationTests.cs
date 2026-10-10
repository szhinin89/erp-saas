using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ZH-SUPPLIER-CREDIT-APPLY-PAYABLES-02D-C — PostgreSQL 16 real + PostingEngine real + MediatR con
/// escaneo de ensamblado + reglas del <see cref="AccountingBootstrapStep"/> real. Un
/// <see cref="SupplierCredit"/> aplica contra CxP de Compra (Lock A "PurchaseInvoice.FinancialLock",
/// moneda de la factura) y de Gasto (concurrencia optimista xmin de AccountsPayable, moneda de la
/// empresa) con el MISMO handler; aplicación parcial, varias aplicaciones, reversas, posting
/// SupplierCreditApplied/ApplicationReversed espejo, rechazos (proveedor/moneda/empresa/anulada) y
/// concurrencia sin sobreaplicación sobre una CxP de Gasto.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierCreditApplyPayablesIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_supplier_credit_apply_payables_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();

    // Misma fecha fija que AlwaysTodayCompanyClock: el bootstrap siembra el período de ese año.
    private readonly DateOnly _today = new(2026, 9, 17);
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherCompanyId;
    private Guid _branchId;
    private Guid _otherBranchId;
    private Guid _supplierId;
    private Guid _otherSupplierId;
    private Guid _paymentTermId;
    private Guid _payablesLedgerAccountId;
    private Guid _advancesLedgerAccountId;
    private Guid _bankLedgerAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        var otherCompany = Company.CreateManaged(
            tenant.Id,
            "1790098765001",
            "Otra S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherCompanyId = otherCompany.Id;

        var branch = NewBranch(_companyId, "001");
        var otherBranch = NewBranch(_otherCompanyId, "002");
        var supplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        var otherSupplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034073",
            1,
            "Otro Proveedor",
            _userId
        );
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        db.Branches.AddRange(branch, otherBranch);
        db.BusinessPartners.AddRange(supplier, otherSupplier);
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;
        _supplierId = supplier.Id;
        _otherSupplierId = otherSupplier.Id;
        _paymentTermId = paymentTerm.Id;

        await new AccountingBootstrapStep(
            db,
            new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(),
            NullLogger<AccountingBootstrapStep>.Instance
        ).ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _userId));
        var accounts = await db
            .Accounts.Where(a => a.CompanyId == _companyId)
            .ToDictionaryAsync(a => a.Code.Value, a => a.Id);
        _payablesLedgerAccountId = accounts["2.1.01.001"];
        _advancesLedgerAccountId = accounts["1.1.03.004"];
        _bankLedgerAccountId = accounts["1.1.02.001"];
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private Branch NewBranch(Guid companyId, string code) =>
        Branch.Create(
            _tenantId,
            $"Sucursal {code}",
            "Av. Principal 123",
            code,
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
            _userId,
            companyId: companyId
        );

    private ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );

    /// <summary>Mismo mecanismo que SupplierCreditRefundPostingIntegrationTests.BuildWiredContext.</summary>
    private ErpDbContext BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var company = new FixedCurrentCompany(() => _companyId);
        var db = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            deferred,
            company
        );

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
        services.AddScoped<ISupplierCreditRepository>(_ => new SupplierCreditRepository(
            db,
            company
        ));
        services.AddScoped<ERP.Domain.Modules.Finance.Interfaces.ISupplierCreditRefundTransactionRepository>(
            _ => new ERP.Infrastructure.Persistence.Repositories.Finance.SupplierCreditRefundTransactionRepository(
                db,
                company
            )
        );
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => _tenantId,
            () => _companyId,
            _userId
        ));
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(SupplierCreditAppliedPostingTranslator).Assembly
            )
        );

        deferred.Inner = services.BuildServiceProvider().GetRequiredService<IPublisher>();
        return db;
    }

    private ApplySupplierCreditHandler ApplyHandler(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(() => _companyId);
        return new(
            new SupplierCreditRepository(db, company),
            new AccountsPayableRepository(db),
            new PurchaseInvoiceRepository(db, company),
            new PurchaseReturnRepository(db, company),
            new CompanyRepository(db),
            new UnitOfWork(db),
            new PostgresDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
    }

    private ReverseSupplierCreditApplicationHandler ReverseHandler(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(() => _companyId);
        return new(
            new SupplierCreditRepository(db, company),
            new AccountsPayableRepository(db),
            new PurchaseReturnRepository(db, company),
            new UnitOfWork(db),
            new PostgresDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );
    }

    // ── Seeds ──────────────────────────────────────────────────────────────

    /// <summary>Saldo a favor (origen pago a proveedor sin CxP, anticipo íntegro).</summary>
    private async Task<Guid> SeedCreditAsync(
        decimal amount,
        Guid? supplierId = null,
        string currency = "USD"
    )
    {
        await using var db = CreateContext();
        var payment = SupplierPayment.Create(
            _tenantId,
            _companyId,
            _branchId,
            supplierId ?? _supplierId,
            _today,
            amount,
            $"SP-{Guid.NewGuid():N}"[..12],
            null,
            [
                new SupplierPaymentMethodLineInput(
                    await EnsureTransferMethodAsync(db),
                    await EnsureBankAccountAsync(db),
                    null,
                    amount,
                    "OP-1",
                    TransactionDate: _today
                ),
            ],
            [],
            [],
            _userId,
            unappliedAmountConfirmed: true,
            allowWithoutPayable: true
        );
        var credit = SupplierCredit.CreateFromSupplierPayment(
            _tenantId,
            _companyId,
            _branchId,
            supplierId ?? _supplierId,
            currency,
            payment.Id,
            payment.UnappliedAmount,
            _userId
        );
        db.SupplierPayments.Add(payment);
        db.Set<SupplierCredit>().Add(credit);
        await db.SaveChangesAsync();
        return credit.Id;
    }

    private Guid? _transferMethodId;
    private Guid? _bankAccountId;

    private async Task<Guid> EnsureTransferMethodAsync(ErpDbContext db)
    {
        if (_transferMethodId is { } id)
            return id;
        var method = ERP.Domain.Modules.Sales.Entities.PaymentMethod.Create(
            _tenantId,
            "TRANSFER",
            "Transferencia",
            true,
            false,
            1,
            _userId,
            ERP.Domain.Modules.Sales.Enums.PaymentMethodDetailType.Transfer
        );
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();
        return (_transferMethodId = method.Id).Value;
    }

    private async Task<Guid> EnsureBankAccountAsync(ErpDbContext db)
    {
        if (_bankAccountId is { } id)
            return id;
        var bank = ERP.Domain.MasterData.Entities.Bank.Create(
            _tenantId,
            "PICHINCHA",
            "Banco Pichincha",
            "Pichincha",
            _userId
        );
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        var account = ERP.Domain.Modules.Finance.Entities.CompanyBankAccount.Create(
            _tenantId,
            _companyId,
            bank.Id,
            ERP.Domain.Modules.Finance.Enums.BankAccountType.Checking,
            "2200123456",
            "Banco Pichincha CTE",
            _bankLedgerAccountId,
            _userId
        );
        db.CompanyBankAccounts.Add(account);
        await db.SaveChangesAsync();
        return (_bankAccountId = account.Id).Value;
    }

    /// <summary>CxP de Compra real (factura confirmada — la moneda sale de PurchaseInvoice).</summary>
    private async Task<Guid> SeedPurchasePayableAsync(decimal total)
    {
        await using var db = CreateContext();
        var invoice = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1710034065001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            _today,
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        invoice.ReplaceLines(
            [
                PurchaseInvoiceDetail.Create(
                    invoice.Id,
                    _tenantId,
                    "Producto",
                    quantity: 1m,
                    unitPrice: total,
                    vatCode: "0",
                    uomCode: "UNIT"
                ),
            ],
            _userId
        );
        invoice.Confirm(_userId);
        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            invoice.Id,
            "01",
            invoice.InvoiceNumber,
            _today,
            _today,
            _userId
        );
        payable.AddInstallment(1, _today.AddDays(30), invoice.GrandTotal);
        db.PurchaseInvoices.Add(invoice);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return payable.Id;
    }

    /// <summary>CxP de Gasto (moneda = Company.CurrencyCode; el gasto no tiene FK desde la CxP).</summary>
    private async Task<Guid> SeedExpensePayableAsync(
        decimal total,
        Guid? companyId = null,
        Guid? branchId = null,
        bool cancelled = false
    )
    {
        await using var db = CreateContext();
        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            companyId ?? _companyId,
            branchId ?? _branchId,
            _supplierId,
            AccountsPayableOriginType.ExpenseDocument,
            Guid.NewGuid(),
            "EXP",
            $"GAS-{Random.Shared.Next(100000, 999999)}",
            _today,
            _today,
            _userId
        );
        payable.AddInstallment(1, _today.AddDays(30), total);
        if (cancelled)
            payable.Cancel(_userId);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return payable.Id;
    }

    /// <summary>
    /// IL-6B — CxP InitialBalance real (Carga Inicial de CxP): sin compra ni gasto; la fila del lote es
    /// su origen y la moneda es la de la empresa.
    /// </summary>
    private async Task<Guid> SeedInitialBalancePayableAsync(decimal balance)
    {
        await using var db = CreateContext();
        var batch = ImportBatch.Create(_tenantId, _companyId, ImportType.InitialPayables, _userId);
        db.ImportBatches.Add(batch);
        var payable = AccountsPayable.CreateInitialBalance(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            _today.AddDays(-60),
            _today.AddDays(30),
            _today.AddDays(-1),
            balance,
            batch.Id,
            Guid.NewGuid(),
            _userId
        );
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();
        return payable.Id;
    }

    // ── Ejecución ──────────────────────────────────────────────────────────

    private async Task<Result<SupplierCreditDto>> ApplyAsync(
        Guid creditId,
        Guid payableId,
        decimal amount
    )
    {
        await using var db = BuildWiredContext();
        return await ApplyHandler(db)
            .Handle(
                new ApplySupplierCreditCommand(creditId, payableId, amount, Guid.NewGuid()),
                CancellationToken.None
            );
    }

    private async Task<Result<SupplierCreditDto>> ReverseAsync(
        Guid creditId,
        Guid movementId,
        Guid payableId
    )
    {
        await using var db = BuildWiredContext();
        return await ReverseHandler(db)
            .Handle(
                new ReverseSupplierCreditApplicationCommand(
                    creditId,
                    movementId,
                    payableId,
                    Guid.NewGuid()
                ),
                CancellationToken.None
            );
    }

    private async Task<(
        decimal Available,
        decimal PayableCredit,
        decimal Outstanding,
        AccountsPayableStatus Status
    )> StateAsync(Guid creditId, Guid payableId)
    {
        await using var db = CreateContext();
        var credit = await db.Set<SupplierCredit>()
            .AsNoTracking()
            .SingleAsync(c => c.Id == creditId);
        var payable = await db
            .AccountsPayables.IgnoreQueryFilters()
            .Include(p => p.Installments)
            .AsNoTracking()
            .SingleAsync(p => p.Id == payableId);
        return (
            credit.AvailableAmount,
            payable.SupplierCreditAmount,
            payable.OutstandingAmount,
            payable.Status
        );
    }

    private async Task<List<(Guid AccountId, decimal Debit, decimal Credit)>> JournalLinesAsync(
        Guid movementId,
        string factType
    )
    {
        await using var db = CreateContext();
        var entries = await db
            .JournalEntries.Include(j => j.Lines)
            .Where(j =>
                j.SourceEventId == movementId
                && j.SourceModule == "Purchases"
                && j.SourceEventType == factType
            )
            .ToListAsync();
        entries.Should().ContainSingle($"exactamente un asiento {factType} por movimiento");
        return entries[0].Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).ToList();
    }

    private static Guid LastApplicationId(SupplierCreditDto dto) =>
        dto
            .Movements.Where(m => m.MovementType == "Application")
            .OrderBy(m => m.CreatedAtUtc)
            .Last()
            .Id;

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aplica_a_CxP_de_Compra_con_asiento_CxP_contra_Anticipos()
    {
        var creditId = await SeedCreditAsync(100m);
        var payableId = await SeedPurchasePayableAsync(80m);

        var result = await ApplyAsync(creditId, payableId, 30m);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await StateAsync(creditId, payableId))
            .Should()
            .Be((70m, 30m, 50m, AccountsPayableStatus.PartiallyPaid));
        (await JournalLinesAsync(LastApplicationId(result.Value!), "SupplierCreditApplied"))
            .Should()
            .BeEquivalentTo(
                new[] { (_payablesLedgerAccountId, 30m, 0m), (_advancesLedgerAccountId, 0m, 30m) }
            );
    }

    [Fact]
    public async Task Aplica_parcialmente_a_CxP_de_Gasto_con_el_mismo_handler_y_asiento()
    {
        var creditId = await SeedCreditAsync(100m);
        var payableId = await SeedExpensePayableAsync(60m);

        var result = await ApplyAsync(creditId, payableId, 25m);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await StateAsync(creditId, payableId))
            .Should()
            .Be((75m, 25m, 35m, AccountsPayableStatus.PartiallyPaid));
        (await JournalLinesAsync(LastApplicationId(result.Value!), "SupplierCreditApplied"))
            .Should()
            .BeEquivalentTo(
                new[] { (_payablesLedgerAccountId, 25m, 0m), (_advancesLedgerAccountId, 0m, 25m) }
            );
    }

    [Fact]
    public async Task IL6B_Aplica_a_CxP_de_saldo_inicial_con_el_mismo_handler_y_asiento_y_su_reversa_es_espejo()
    {
        var creditId = await SeedCreditAsync(100m);
        var payableId = await SeedInitialBalancePayableAsync(60m);

        var result = await ApplyAsync(creditId, payableId, 25m);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await StateAsync(creditId, payableId))
            .Should()
            .Be((75m, 25m, 35m, AccountsPayableStatus.PartiallyPaid));
        var movementId = LastApplicationId(result.Value!);
        (await JournalLinesAsync(movementId, "SupplierCreditApplied"))
            .Should()
            .BeEquivalentTo(
                new[] { (_payablesLedgerAccountId, 25m, 0m), (_advancesLedgerAccountId, 0m, 25m) }
            );
        await using (var db = CreateContext())
        {
            (await db.PurchaseInvoices.IgnoreQueryFilters().CountAsync(i => i.TenantId == _tenantId))
                .Should().Be(0, "nunca se inventa una compra de origen");
            (await db.ExpenseDocuments.IgnoreQueryFilters().CountAsync(e => e.TenantId == _tenantId))
                .Should().Be(0, "nunca se inventa un gasto de origen");
        }

        var reverse = await ReverseAsync(creditId, movementId, payableId);

        reverse.IsSuccess.Should().BeTrue(reverse.Error);
        (await StateAsync(creditId, payableId))
            .Should()
            .Be((100m, 0m, 60m, AccountsPayableStatus.Pending));
    }

    [Fact]
    public async Task Un_saldo_se_aplica_a_Compra_y_Gasto_y_ambas_reversas_son_espejo()
    {
        var creditId = await SeedCreditAsync(100m);
        var purchasePayableId = await SeedPurchasePayableAsync(40m);
        var expensePayableId = await SeedExpensePayableAsync(30m);

        var toPurchase = await ApplyAsync(creditId, purchasePayableId, 40m);
        toPurchase.IsSuccess.Should().BeTrue(toPurchase.Error);
        var purchaseMovementId = LastApplicationId(toPurchase.Value!);
        var toExpense = await ApplyAsync(creditId, expensePayableId, 30m);
        toExpense.IsSuccess.Should().BeTrue(toExpense.Error);
        var expenseMovementId = LastApplicationId(toExpense.Value!);

        (await StateAsync(creditId, purchasePayableId))
            .Should()
            .Be((30m, 40m, 0m, AccountsPayableStatus.Paid));
        (await StateAsync(creditId, expensePayableId))
            .Should()
            .Be((30m, 30m, 0m, AccountsPayableStatus.Paid));

        (await ReverseAsync(creditId, expenseMovementId, expensePayableId))
            .IsSuccess.Should()
            .BeTrue();
        (await StateAsync(creditId, expensePayableId))
            .Should()
            .Be((60m, 0m, 30m, AccountsPayableStatus.Pending));
        (await ReverseAsync(creditId, purchaseMovementId, purchasePayableId))
            .IsSuccess.Should()
            .BeTrue();
        (await StateAsync(creditId, purchasePayableId))
            .Should()
            .Be((100m, 0m, 40m, AccountsPayableStatus.Pending));

        await using var db = CreateContext();
        var reversals = await db.Set<SupplierCreditMovement>()
            .AsNoTracking()
            .Where(m => m.SupplierCreditId == creditId && m.ReversalOfMovementId != null)
            .ToListAsync();
        foreach (
            var (reversal, amount) in new[]
            {
                (reversals.Single(r => r.ReversalOfMovementId == expenseMovementId), 30m),
                (reversals.Single(r => r.ReversalOfMovementId == purchaseMovementId), 40m),
            }
        )
        {
            (await JournalLinesAsync(reversal.Id, "SupplierCreditApplicationReversed"))
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (_advancesLedgerAccountId, amount, 0m),
                        (_payablesLedgerAccountId, 0m, amount),
                    }
                );
        }
    }

    [Fact]
    public async Task Proveedor_distinto_se_rechaza_sin_efectos()
    {
        var creditId = await SeedCreditAsync(100m, supplierId: _otherSupplierId);
        var payableId = await SeedExpensePayableAsync(50m);

        var result = await ApplyAsync(creditId, payableId, 10m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("proveedor distinto");
        (await StateAsync(creditId, payableId))
            .Should()
            .Be((100m, 0m, 50m, AccountsPayableStatus.Pending));
    }

    [Fact]
    public async Task Moneda_distinta_a_la_de_la_empresa_se_rechaza_en_CxP_de_Gasto()
    {
        var creditId = await SeedCreditAsync(100m, currency: "EUR");
        var payableId = await SeedExpensePayableAsync(50m);

        var result = await ApplyAsync(creditId, payableId, 10m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("moneda");
        (await StateAsync(creditId, payableId))
            .Should()
            .Be((100m, 0m, 50m, AccountsPayableStatus.Pending));
    }

    [Fact]
    public async Task CxP_de_otra_empresa_del_mismo_tenant_se_rechaza_sin_efectos()
    {
        var creditId = await SeedCreditAsync(100m);
        var foreignPayableId = await SeedExpensePayableAsync(50m, _otherCompanyId, _otherBranchId);

        var result = await ApplyAsync(creditId, foreignPayableId, 10m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La cuenta por pagar destino no existe.");
        (await StateAsync(creditId, foreignPayableId))
            .Should()
            .Be((100m, 0m, 50m, AccountsPayableStatus.Pending));
    }

    [Fact]
    public async Task CxP_de_Gasto_anulada_se_rechaza()
    {
        var creditId = await SeedCreditAsync(100m);
        var payableId = await SeedExpensePayableAsync(50m, cancelled: true);

        var result = await ApplyAsync(creditId, payableId, 10m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");
        (await StateAsync(creditId, payableId)).Available.Should().Be(100m);
    }

    [Fact]
    public async Task Aplicaciones_concurrentes_de_dos_saldos_sobre_la_misma_CxP_de_Gasto_nunca_sobreaplican()
    {
        var creditA = await SeedCreditAsync(100m);
        var creditB = await SeedCreditAsync(100m);
        var payableId = await SeedExpensePayableAsync(50m);

        var results = await Task.WhenAll(
            Task.Run(() => ApplyAsync(creditA, payableId, 40m)),
            Task.Run(() => ApplyAsync(creditB, payableId, 40m))
        );

        results
            .Count(r => r.IsSuccess)
            .Should()
            .Be(1, "solo cabe una aplicación de 40 en un saldo pendiente de 50");
        var (availableA, payableCredit, outstanding, _) = await StateAsync(creditA, payableId);
        var (availableB, _, _, _) = await StateAsync(creditB, payableId);
        payableCredit.Should().Be(40m);
        outstanding.Should().Be(10m);
        (availableA + availableB).Should().Be(160m, "el saldo que falló no se consumió");

        await using var db = CreateContext();
        (await db.JournalEntries.CountAsync(j => j.SourceEventType == "SupplierCreditApplied"))
            .Should()
            .Be(1, "un único asiento para la única aplicación persistida");
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

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
