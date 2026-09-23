using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — suite de integración
/// (PostgreSQL 16 real vía Testcontainers, mismo patrón que
/// <c>SalesInvoiceAuthorizedPostingIntegrationTests</c> de los Lotes 1/2/6A, archivo separado para
/// no tocar esa suite ya cerrada) que cubre los 2 objetivos de este lote contra el asiento
/// "Sales"/"InvoiceIssued" REAL sembrado por <c>AccountingBootstrapStep</c> (no una regla de
/// prueba hecha a mano): (1) el Debe se separa en Caja/CxC según el dinero real cobrado, (2)
/// anular la venta reversa los asientos "InvoiceIssued"/"CostOfGoodsSold" ya generados.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesInvoiceCashReceivableSplitAndCancelReversalIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sales_cash_split_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _createdBy;

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
        var customer = BusinessPartner.Create(tenant.Id, "05", "1710034065", 1, "Cliente Test", _createdBy);
        var establishment = Establishment.Create(
            tenant.Id,
            branchId: branch.Id,
            company.Id,
            code: "001",
            name: "Matriz Test",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            tenant.Id, company.Id, branch.Id, "CAJA-01", "Caja Principal", _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id, company.Id, establishment.Id, "001", "PE-001", EmissionType.Electronic, true, _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id, company.Id, branch.Id, _createdBy, cashRegister.Id, "CAJA-01", "Caja Principal",
            emissionPoint.Id, "001", 0m, _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;

        // Seed real de AccountingBootstrapStep — usa la PostingRule "Sales"/"InvoiceIssued" tal
        // como queda configurada en producción (5 líneas: Caja/CxC condicionales + Subtotal/
        // TaxVat/TaxIce), no una regla artificial de test.
        var bootstrap = new ERP.Infrastructure.Seeding.Steps.AccountingBootstrapStep(
            db,
            new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<
                ERP.Infrastructure.Seeding.Steps.AccountingBootstrapStep
            >.Instance
        );
        await bootstrap.ExecuteAsync(
            new ERP.Application.Common.Interfaces.CompanyBootstrapContext(_tenantId, _companyId, _createdBy)
        );
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

    private (ErpDbContext db, IPublisher publisher) BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
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
        services.AddSingleton<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>(
            ERP.Infrastructure.Tests.TestData.StandardPrecisionPolicyProvider.Instance
        );
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(_companyId));
        // ReverseJournalEntryCommandHandler (invocado por SalesInvoiceCancelledPostingTranslator)
        // necesita ICurrentUser para SetUpdated/CreatedBy del asiento de reverso.
        services.AddSingleton<ICurrentUser>(new FixedCurrentUser(_createdBy));
        services.AddScoped<
            ICashSessionRepository,
            ERP.Infrastructure.Persistence.Repositories.Caja.CashSessionRepository
        >();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddScoped<
            ERP.Application.Common.Persistence.IDatabaseExceptionTranslator,
            ERP.Infrastructure.Persistence.PostgresDatabaseExceptionTranslator
        >();
        services.AddScoped<
            ERP.Domain.Modules.Inventory.Interfaces.IStockRepository,
            ERP.Infrastructure.Persistence.Repositories.Inventory.StockRepository
        >();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SalesInvoiceAuthorizedPostingTranslator).Assembly)
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    private async Task SeedPeriodAsync(ErpDbContext db, DateOnly entryDate)
    {
        var hasPeriod = await db.AccountingPeriods.AnyAsync(p =>
            p.CompanyId == _companyId && p.FiscalYear == entryDate.Year
        );
        if (hasPeriod)
            return;

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, 1, 1),
            new DateOnly(entryDate.Year, 12, 31),
            _createdBy
        );
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private SalesInvoice BuildAuthorizableInvoice(
        DateOnly issueDate,
        string invoiceNumber,
        decimal unitPrice,
        decimal cashPortion
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            _tenantId, _companyId, _branchId, _customerId, customer,
            invoiceNumber: invoiceNumber, issueDate: issueDate, createdBy: _createdBy,
            paymentTerm: paymentTerm, cashSessionId: _cashSessionId, emissionPointId: null
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id, _tenantId, "Producto Test", quantity: 1, unitPrice: unitPrice, vatCode: "0", uomCode: "UNIT"
        );
        line.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _createdBy);

        var payments = new List<SalesInvoicePayment>();
        if (cashPortion > 0)
            payments.Add(
                SalesInvoicePayment.Create(inv.Id, _tenantId, Guid.NewGuid(), "01", "Efectivo", cashPortion)
            );
        var creditPortion = unitPrice - cashPortion;
        if (creditPortion > 0)
            payments.Add(
                SalesInvoicePayment.Create(inv.Id, _tenantId, Guid.NewGuid(), "20", "Crédito", creditPortion)
            );
        inv.ReplacePayments(payments, _createdBy);

        return inv;
    }

    // Método de pago "Crédito" (código SRI "20") debe existir y estar marcado
    // IsCreditAllowed=true para que AuthorizeSalesInvoiceHandler lo excluya de cashApplied — este
    // test llama a inv.Authorize(uid, cashApplied) directamente (nivel de dominio, mismo patrón
    // que la suite de Lotes 1/2/6A), así que no depende de PaymentMethod/IPaymentMethodRepository.

    [Fact]
    public async Task Descuento_subcentavo_persiste_sin_lineas_cero_y_se_reversa_tras_recargar()
    {
        var issueDate = new DateOnly(2026, 9, 13);
        Guid invoiceId;
        Guid originalEntryId;

        var (postingDb, _) = BuildWiredContext();
        await using (postingDb)
        {
            await SeedPeriodAsync(postingDb, issueDate);
            var invoice = BuildAuthorizableInvoice(
                issueDate, "001-001-000000107", 0.30m, cashPortion: 0.30m
            );
            var line = invoice.Lines.Single();
            line.ApplyDiscount(1.15m);
            line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
            invoice.ReplacePayments(
                new[] { SalesInvoicePayment.Create(
                    invoice.Id, _tenantId, Guid.NewGuid(), "01", "Efectivo", 0.35m
                ) },
                _createdBy
            );

            line.DiscountAmount.Should().Be(0.003450m);
            line.TaxableBase.Should().Be(0.30m);
            invoice.Subtotal.Should().Be(0.303450m);
            invoice.TotalVat.Should().Be(0.05m);
            invoice.GrandTotal.Should().Be(0.35m);

            postingDb.SalesInvoices.Add(invoice);
            await postingDb.SaveChangesAsync();
            invoice.Authorize(_createdBy, cashApplied: 0.35m);
            await postingDb.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        // A new context is essential: PostgreSQL numeric(18,2), not tracked CLR values,
        // must be the source of both the assertions and the cancellation/reversal.
        var (cancellationDb, _) = BuildWiredContext();
        await using (cancellationDb)
        {
            var original = await cancellationDb.JournalEntries.Include(x => x.Lines)
                .SingleAsync(x => x.SourceEventId == invoiceId && x.SourceEventType == "InvoiceIssued");
            originalEntryId = original.Id;
            original.Status.Should().Be(JournalEntryStatus.Posted);
            original.Lines.Should().HaveCount(3);
            original.Lines.Should().OnlyContain(l => l.Debit > 0m || l.Credit > 0m);
            original.Lines.Sum(l => l.Debit).Should().Be(0.35m);
            original.Lines.Sum(l => l.Credit).Should().Be(0.35m);
            var accounts = await cancellationDb.Accounts
                .Where(a => a.CompanyId == _companyId).ToListAsync();
            var discountAccount = accounts.Single(a => a.Code.Value == "4.1.02.001");
            original.Lines.Should().NotContain(l => l.AccountId == discountAccount.Id);

            var invoice = await cancellationDb.SalesInvoices
                .Include(x => x.Lines).Include(x => x.Payments)
                .SingleAsync(x => x.Id == invoiceId);
            invoice.Cancel("Regresión descuento subcentavo", _createdBy);
            await cancellationDb.SaveChangesAsync();
        }

        await using var verifyDb = CreateContext();
        var persistedOriginal = await verifyDb.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.Id == originalEntryId);
        persistedOriginal.Status.Should().Be(JournalEntryStatus.Reversed);
        persistedOriginal.ReverseJournalEntryId.Should().NotBeNull();
        var reversal = await verifyDb.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.Id == persistedOriginal.ReverseJournalEntryId);
        reversal.Status.Should().Be(JournalEntryStatus.Posted);
        reversal.OriginalJournalEntryId.Should().Be(originalEntryId);
        reversal.Lines.Should().HaveCount(3);
        reversal.Lines.Should().OnlyContain(l => l.Debit > 0m || l.Credit > 0m);
        reversal.Lines.Sum(l => l.Debit).Should().Be(0.35m);
        reversal.Lines.Sum(l => l.Credit).Should().Be(0.35m);
        reversal.Lines.Select(l => new { l.AccountId, l.Debit, l.Credit }).Should()
            .BeEquivalentTo(persistedOriginal.Lines.Select(l => new {
                l.AccountId, Debit = l.Credit, Credit = l.Debit
            }));
    }

    [Fact]
    public async Task Venta_100_por_ciento_contado_genera_Debe_Caja_sin_ninguna_linea_de_CxC()
    {
        var issueDate = new DateOnly(2026, 9, 13);
        var (db, _) = BuildWiredContext();
        await SeedPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000101", 100m, cashPortion: 100m);
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy, cashApplied: 100m);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceEventId == inv.Id && x.SourceEventType == "InvoiceIssued");
        entry.Status.Should().Be(JournalEntryStatus.Posted);

        var accountCodesById = await verifyDb.Accounts
            .Where(a => a.CompanyId == _companyId)
            .ToDictionaryAsync(a => a.Id, a => a.Code.Value);
        var debitLines = entry.Lines.Where(l => l.Debit > 0).ToList();
        debitLines.Should().HaveCount(1, "venta 100% contado: solo Caja, ninguna CxC ficticia");
        accountCodesById[debitLines[0].AccountId].Should().Be("1.1.01.001", "Caja general");
        debitLines[0].Debit.Should().Be(100m);

        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task Venta_100_por_ciento_credito_genera_Debe_CxC_sin_movimiento_de_caja()
    {
        var issueDate = new DateOnly(2026, 9, 13);
        var (db, _) = BuildWiredContext();
        await SeedPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000102", 100m, cashPortion: 0m);
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy, cashApplied: 0m);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceEventId == inv.Id && x.SourceEventType == "InvoiceIssued");

        var accountCodesById = await verifyDb.Accounts
            .Where(a => a.CompanyId == _companyId)
            .ToDictionaryAsync(a => a.Id, a => a.Code.Value);
        var debitLines = entry.Lines.Where(l => l.Debit > 0).ToList();
        debitLines.Should().HaveCount(1, "venta 100% crédito: solo CxC, sin línea de Caja");
        accountCodesById[debitLines[0].AccountId].Should().Be("1.1.03.001", "CxC clientes");
        debitLines[0].Debit.Should().Be(100m);

        // Lote 1 — sin movimiento de caja para la porción a crédito (CashSession.Open ya crea su
        // propio movimiento "Opening" al iniciar la sesión, en InitializeAsync — no relacionado
        // con esta venta; se excluye explícitamente para no producir un falso negativo).
        var cashMovements = await verifyDb.CashMovements
            .Where(m =>
                m.CashSessionId == _cashSessionId
                && m.MovementType != ERP.Domain.Modules.Caja.Enums.CashMovementType.Opening
            )
            .ToListAsync();
        cashMovements.Should().BeEmpty();
    }

    [Fact]
    public async Task Venta_parcial_genera_Debe_Caja_mas_Debe_CxC_balanceado()
    {
        var issueDate = new DateOnly(2026, 9, 13);
        var (db, _) = BuildWiredContext();
        await SeedPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000103", 100m, cashPortion: 40m);
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy, cashApplied: 40m);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstAsync(x => x.SourceEventId == inv.Id && x.SourceEventType == "InvoiceIssued");

        var accountCodesById = await verifyDb.Accounts
            .Where(a => a.CompanyId == _companyId)
            .ToDictionaryAsync(a => a.Id, a => a.Code.Value);
        var debitLines = entry.Lines.Where(l => l.Debit > 0).ToList();
        debitLines.Should().HaveCount(2, "venta parcial: Caja + CxC");
        debitLines.Should().Contain(l => accountCodesById[l.AccountId] == "1.1.01.001" && l.Debit == 40m);
        debitLines.Should().Contain(l => accountCodesById[l.AccountId] == "1.1.03.001" && l.Debit == 60m);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit)).And.Be(100m);

        var receivable = await verifyDb.SalesReceivables.SingleOrDefaultAsync(r => r.InvoiceId == inv.Id);
        // Este test opera a nivel de dominio (Authorize() directo) — SalesReceivable la crea
        // AuthorizeSalesInvoiceHandler (Application), no SalesInvoice.Authorize() (Domain). Se
        // confirma en su lugar el saldo pendiente correcto queda reflejado en el asiento (arriba).
        receivable.Should().BeNull();
    }

    [Fact]
    public async Task Anular_venta_contado_reversa_InvoiceIssued_y_CostOfGoodsSold()
    {
        var issueDate = new DateOnly(2026, 9, 13);
        var (db, _) = BuildWiredContext();
        await SeedPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000104", 100m, cashPortion: 100m);
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy, cashApplied: 100m);
        await db.SaveChangesAsync();

        await using (var verifyBeforeDb = CreateContext())
        {
            var before = await verifyBeforeDb.JournalEntries.FirstAsync(x =>
                x.SourceEventId == inv.Id && x.SourceEventType == "InvoiceIssued"
            );
            before.Status.Should().Be(JournalEntryStatus.Posted);
        }

        inv.Cancel("Cliente se arrepintió", _createdBy);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var reversedInvoiceIssued = await verifyDb.JournalEntries.FirstAsync(x =>
            x.SourceEventId == inv.Id && x.SourceEventType == "InvoiceIssued"
        );
        reversedInvoiceIssued.Status.Should().Be(JournalEntryStatus.Reversed);
        reversedInvoiceIssued.ReverseJournalEntryId.Should().NotBeNull();

        var reversalEntry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.Id == reversedInvoiceIssued.ReverseJournalEntryId);
        reversalEntry.Status.Should().Be(JournalEntryStatus.Posted);
        reversalEntry.Lines.Sum(l => l.Debit).Should().Be(reversalEntry.Lines.Sum(l => l.Credit));

        // Venta sin líneas de inventario en este test (Item sin ItemId/WarehouseId) — nunca se
        // publicó CostOfGoodsSold, así que no hay nada que reversar para ese FactType (documentado
        // explícitamente en SalesInvoiceCancelledPostingTranslator). Se confirma que la ausencia no
        // genera error ni entrada espuria.
        var cogsEntries = await verifyDb.JournalEntries
            .Where(x => x.SourceEventId == inv.Id && x.SourceEventType == "CostOfGoodsSold")
            .ToListAsync();
        cogsEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Anular_venta_a_credito_reversa_el_asiento_de_CxC()
    {
        var issueDate = new DateOnly(2026, 9, 13);
        var (db, _) = BuildWiredContext();
        await SeedPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000105", 100m, cashPortion: 0m);
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy, cashApplied: 0m);
        await db.SaveChangesAsync();

        inv.Cancel("Anulación de venta a crédito", _createdBy);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var reversed = await verifyDb.JournalEntries.FirstAsync(x =>
            x.SourceEventId == inv.Id && x.SourceEventType == "InvoiceIssued"
        );
        reversed.Status.Should().Be(JournalEntryStatus.Reversed);
    }

    [Fact]
    public async Task Reversar_dos_veces_no_es_posible_segunda_anulacion_no_reversa_de_nuevo()
    {
        // Idempotencia a nivel de dominio: SalesInvoice.Cancel() ya rechaza una segunda anulación
        // (EnsureDraft/status guard), así que este escenario nunca alcanza a re-disparar el
        // translator de reverso — se confirma end-to-end contra Postgres real.
        var issueDate = new DateOnly(2026, 9, 13);
        var (db, _) = BuildWiredContext();
        await SeedPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000106", 100m, cashPortion: 100m);
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy, cashApplied: 100m);
        await db.SaveChangesAsync();

        inv.Cancel("Primera anulación", _createdBy);
        await db.SaveChangesAsync();

        var act = () => inv.Cancel("Segunda anulación", _createdBy);
        act.Should().Throw<InvalidOperationException>();

        await using var verifyDb = CreateContext();
        var entries = await verifyDb.JournalEntries
            .Where(x => x.SourceEventId == inv.Id && x.SourceEventType == "InvoiceIssued")
            .ToListAsync();
        entries.Should().HaveCount(1, "un único asiento original, sin reverso duplicado");
        entries[0].Status.Should().Be(JournalEntryStatus.Reversed);

        var reversals = await verifyDb.JournalEntries
            .Where(x => x.SourceEventType == "Reversal" && x.SourceEventId == entries[0].Id)
            .ToListAsync();
        reversals.Should().HaveCount(1, "un único asiento de reverso, nunca dos por la misma factura");
    }

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
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

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "test-user";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
