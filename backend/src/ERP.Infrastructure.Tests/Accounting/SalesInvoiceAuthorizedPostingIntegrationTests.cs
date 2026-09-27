using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Events;
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
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el primer consumidor real
/// del Posting Engine (Fase 3.3, ADR-026 §8): SalesInvoice.Authorize() → SalesInvoiceAuthorizedEvent
/// → SalesInvoiceAuthorizedPostingTranslator → IPostingEngine → JournalEntry Draft. Usa un
/// contenedor DI real (AddMediatR con escaneo de ensamblado) para confirmar que el registro
/// automático de INotificationHandler&lt;SalesInvoiceAuthorizedEvent&gt; funciona sin configuración
/// manual. Requiere Docker.
/// </summary>
/// <remarks>
/// Usa <see cref="ERP.Infrastructure.Persistence.Repositories.Caja.CashSessionRepository"/> real
/// (no un stub) — el hallazgo crítico de Fase 3.3 (re-entrancia de <c>SaveChangesAsync</c> cuando
/// <see cref="SalesInvoiceAuthorizedHandler"/> (Caja) y el Translator de Accounting reaccionan al
/// mismo evento en el mismo ciclo de <c>Publish</c>) quedó resuelto en Fase 3.3.5:
/// <c>PostingPipeline</c> ya no llama a <c>SaveChangesAsync</c> internamente (solo hace staging
/// vía <c>AddAsync</c>), por lo que ya no re-entra en <c>ErpDbContext.SaveChangesAsync</c>
/// mientras el ciclo externo todavía está en curso.
/// </remarks>
[Trait("Category", "PostgreSql")]
public sealed class SalesInvoiceAuthorizedPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sales_posting_test")
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
        var customer = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Cliente Test",
            _createdBy
        );
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
            tenant.Id,
            company.Id,
            branch.Id,
            "CAJA-01",
            "Caja Principal",
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id,
            company.Id,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id,
            company.Id,
            branch.Id,
            _createdBy,
            cashRegister.Id,
            "CAJA-01",
            "Caja Principal",
            emissionPoint.Id,
            "001",
            0m,
            _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
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

    /// <summary>Construye un contenedor DI real (mismo mecanismo de producción: AddMediatR con
    /// escaneo de ensamblado) apuntando siempre a la misma instancia de ErpDbContext, para que
    /// el Translator y el handler de Caja operen dentro de la misma transacción/SaveChanges.</summary>
    private static (ErpDbContext db, IPublisher publisher) BuildWiredContext(
        Guid tenantId,
        Guid companyId,
        PostgreSqlContainer postgres
    )
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            // Mismo interceptor que registra la DI de producción (ERP.Infrastructure/
            // DependencyInjection.cs) — sin él, un hijo nuevo (CashMovement) agregado a un
            // agregado ya trackeado (CashSession) dentro de un domain event handler queda mal
            // clasificado como Modified no-op → DbUpdateConcurrencyException (ADR-020, FROZEN).
            // No relacionado con Posting — es infraestructura de tracking ya existente que este
            // test necesita replicar para reflejar fielmente el comportamiento de producción.
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        var db = new ErpDbContext(
            options,
            new FixedCurrentTenant(tenantId),
            deferred,
            new FixedCurrentCompany(companyId)
        );

        var services = new ServiceCollection();
        services.AddScoped<ERP.Application.Common.Services.ICompanyClock, ERP.Infrastructure.Persistence.Services.CompanyClock>();
        services.AddLogging();
        services.AddSingleton<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>(
            ERP.Infrastructure.Tests.TestData.StandardPrecisionPolicyProvider.Instance
        );
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(companyId));
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
        // SalesInvoiceCogsPostingTranslator (ACCOUNTING-INVENTORY-COGS-07) también escucha
        // SalesInvoiceAuthorizedEvent — el escaneo de ensamblado de AddMediatR lo descubre igual
        // que al Translator principal, así que requiere su propia cadena de dependencias
        // registrada aquí (mismo criterio que PurchaseInvoiceAuditHandler en
        // PurchaseInvoiceConfirmedPostingIntegrationTests). IDatabaseExceptionTranslator usa la
        // implementación real (sin estado, sin dependencias) porque no hay excepción de unicidad
        // que traducir en este flujo — no se necesita un doble de prueba.
        services.AddScoped<
            ERP.Application.Common.Persistence.IDatabaseExceptionTranslator,
            ERP.Infrastructure.Persistence.PostgresDatabaseExceptionTranslator
        >();
        services.AddScoped<
            ERP.Domain.Modules.Inventory.Interfaces.IStockRepository,
            ERP.Infrastructure.Persistence.Repositories.Inventory.StockRepository
        >();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(SalesInvoiceAuthorizedPostingTranslator).Assembly
            )
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    private async Task SeedRuleAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
    {
        // Fase 3.5.5: PostingRuleLine reemplaza a DebitAccountId/CreditAccountId como fuente real
        // de la partida doble. La factura de este test no aplica IVA (TotalVat=0), por lo que
        // Debit GrandTotal == Credit Subtotal (ambos 100) — balanceado con 2 líneas.
        var debitAccount = Account.Create(
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
        var creditAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"4.1.{Guid.NewGuid():N}"[..8]),
            "Ventas",
            null,
            AccountType.Income,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(debitAccount, creditAccount);

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Sales",
            "InvoiceIssued",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(creditAccount.Id, AccountNature.Credit, PostingAmountKind.Subtotal);

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

    private SalesInvoice BuildAuthorizableInvoice(DateOnly issueDate, string invoiceNumber)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            Guid.NewGuid(),
            "Contado",
            installments: 1,
            daysBetween: 0
        );

        var inv = SalesInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            customer,
            invoiceNumber: invoiceNumber,
            issueDate: issueDate,
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: _cashSessionId,
            emissionPointId: null
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _createdBy);

        // Sin ApplyTaxes() (fuera del alcance de este test — lo hace AuthorizeSalesInvoiceHandler
        // vía ISriTaxResolver), TaxInclusiveTotal = TaxableBase = unitPrice * quantity = 100.
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            _tenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            100m
        );
        inv.ReplacePayments(new[] { payment }, _createdBy);

        return inv;
    }

    /// <summary>
    /// Smoke funcional (checkpoint ADR-032 hasta Fase 6, 2026-08-29) — regla con las 4 líneas
    /// reales de un asiento de venta completo: Debit(GrandTotal), Credit(Subtotal, TaxVat,
    /// TaxIce, TaxIrbpnr). Verifica que Fase 3 (ICE ahora computado desde _taxes en Ventas
    /// también) no rompió el balance del asiento real contra Postgres.
    /// </summary>
    private async Task SeedRuleAndPeriodWithIvaIceIrbpnrAsync(ErpDbContext db, DateOnly entryDate)
    {
        var debitAccount = Account.Create(
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
        var salesAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"4.1.{Guid.NewGuid():N}"[..8]),
            "Ventas",
            null,
            AccountType.Income,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var vatAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]),
            "IVA por pagar",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var iceAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.2.{Guid.NewGuid():N}"[..8]),
            "ICE por pagar",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var irbpnrAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.3.{Guid.NewGuid():N}"[..8]),
            "IRBPNR por pagar",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(debitAccount, salesAccount, vatAccount, iceAccount, irbpnrAccount);

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Sales",
            "InvoiceIssued",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(salesAccount.Id, AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(vatAccount.Id, AccountNature.Credit, PostingAmountKind.TaxVat);
        rule.AddLine(iceAccount.Id, AccountNature.Credit, PostingAmountKind.TaxIce);
        rule.AddLine(irbpnrAccount.Id, AccountNature.Credit, PostingAmountKind.TaxIrbpnr);

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

    private SalesInvoice BuildAuthorizableInvoiceWithIvaIceIrbpnr(
        DateOnly issueDate,
        string invoiceNumber
    )
    {
        var inv = BuildAuthorizableInvoice(issueDate, invoiceNumber);
        var line = inv.Lines[0];

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — mismo orden que Compras: ReplaceTaxes
        // antes de ApplyTaxes.
        line.ReplaceTaxes(
            [
                ERP.Domain.Modules.Sales.Entities.SalesInvoiceDetailTax.Create(
                    line.Id,
                    _tenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.10m,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    0.30m,
                    ERP.Domain.Modules.Sales.Enums.SalesTaxSource.Calculated
                ),
            ]
        );
        // IVA 15% + ICE 10% (Percentage) — Fase 3 completada: IceCode/IceRate/IceAmount ahora
        // computados desde _taxes en Ventas también, nunca un escalar paralelo.
        line.ApplyTaxes("10", 15m, "IVA", "3072", 10m, "ICE bebidas");

        // El pago de contado sembrado por BuildAuthorizableInvoice (100m) ya no cubre el nuevo
        // GrandTotal (100 + 16.5 IVA + 10 ICE + 0.30 IRBPNR) — se reemplaza por el monto correcto.
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            _tenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            inv.GrandTotal
        );
        inv.ReplacePayments(new[] { payment }, _createdBy);

        return inv;
    }

    [Fact]
    public async Task Smoke_IVA_ICE_IRBPNR_juntos_genera_asiento_balanceado_con_totales_correctos()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodWithIvaIceIrbpnrAsync(db, issueDate);

        var inv = BuildAuthorizableInvoiceWithIvaIceIrbpnr(issueDate, "001-001-000000010");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        // Subtotal 100, ICE 10 (10% de 100), IVA 15% sobre (100+10)=110 → 16.5, IRBPNR fijo 0.30.
        inv.TotalIce.Should().Be(10m);
        inv.Subtotal.Should().Be(100m);
        var line = inv.Lines[0];
        line.VatAmount.Should().Be(16.5m);
        line.IceCode.Should().Be("3072");
        line.IceAmount.Should().Be(10m);
        line.IrbpnrAmount.Should().Be(0.30m);
        inv.TotalIrbpnr.Should().Be(0.30m);

        inv.Authorize(_createdBy);
        await db.SaveChangesAsync();

        inv.GrandTotal.Should().Be(100m + 16.5m + 10m + 0.30m);

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(5, "GrandTotal + Subtotal + TaxVat + TaxIce + TaxIrbpnr");
        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);
        totalDebit.Should()
            .Be(
                totalCredit,
                "el asiento debe balancear con IVA+ICE+IRBPNR incluidos tras completar Fase 3"
            );
        totalDebit.Should().Be(inv.GrandTotal);
    }

    /// <summary>
    /// SALES-INVOICE-ROUNDING-SUBTOTAL-GRANDTOTAL-01 (Fase 6A) — reproduce contra PostgreSQL REAL
    /// (Testcontainers, no un stub) el caso exacto de la auditoría previa: dos líneas de
    /// UnitPrice=1.995 (numeric 18,6), cada una redondeando su TaxableBase a 2.00 individualmente.
    /// Antes del fix, AuthorizedSubtotal sumaba LineSubtotal crudo (1.995+1.995=3.99) mientras
    /// AuthorizedGrandTotal ya sumaba TaxInclusiveTotal redondeado por línea (2.00+2.00=4.00) — el
    /// asiento real quedaba con un residuo de sub-centavo, tragado en silencio por la tolerancia
    /// temporal del Lote 2. Este test confirma, leyendo de vuelta desde columnas numeric(18,2)
    /// reales: (1) AuthorizedSubtotal - AuthorizedTotalDiscount + AuthorizedTotalTax ==
    /// AuthorizedGrandTotal exactamente, y (2) el JournalEntry persistido balancea con diferencia
    /// EXACTA de 0.00 (ya sin la tolerancia de 1 centavo, revertida en este mismo lote).
    /// </summary>
    [Fact]
    public async Task Dos_lineas_UnitPrice_1_995_genera_asiento_balanceado_sin_residuo_de_subcentavo()
    {
        var issueDate = new DateOnly(2026, 9, 13);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, issueDate);

        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            Guid.NewGuid(),
            "Contado",
            installments: 1,
            daysBetween: 0
        );
        var inv = SalesInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            customer,
            invoiceNumber: "001-001-000000099",
            issueDate: issueDate,
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: _cashSessionId,
            emissionPointId: null
        );

        var line1 = SalesInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto fracción de centavo 1",
            quantity: 1,
            unitPrice: 1.995m,
            vatCode: "0",
            uomCode: "UNIT"
        );
        line1.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);
        var line2 = SalesInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto fracción de centavo 2",
            quantity: 1,
            unitPrice: 1.995m,
            vatCode: "0",
            uomCode: "UNIT"
        );
        line2.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);
        inv.ReplaceLines(new[] { line1, line2 }, _createdBy);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            _tenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            4.00m
        );
        inv.ReplacePayments(new[] { payment }, _createdBy);

        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.SalesInvoices.FirstAsync(x => x.Id == inv.Id);
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);

        persisted.AuthorizedSubtotal.Should().Be(4.00m);
        persisted.AuthorizedGrandTotal.Should().Be(4.00m);
        (
            persisted.AuthorizedSubtotal!.Value
            - persisted.AuthorizedTotalDiscount!.Value
            + persisted.AuthorizedTotalTax!.Value
        )
            .Should()
            .Be(persisted.AuthorizedGrandTotal!.Value);

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);
        (totalDebit - totalCredit)
            .Should()
            .Be(0.00m, "sin la tolerancia temporal del Lote 2, el asiento debe balancear EXACTO");
    }

    [Fact]
    public async Task Autorizar_SalesInvoice_genera_JournalEntry_Posted()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000001");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x =>
            x.SourceEventId == inv.Id
        );

        entry.Should().NotBeNull();
        // Fase 5.2: el Posting Engine publica (Post()) el asiento antes de persistirlo.
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        entry.PostedAtUtc.Should().NotBeNull();
        entry.SourceModule.Should().Be("Sales");
        entry.SourceEventType.Should().Be("InvoiceIssued");
    }

    // SALES-JOURNAL-ENTRY-SILENT-FAILURE-01 Lote 2 — este test documentaba el comportamiento
    // ANTES del fix: un fallo del Posting Engine dejaba la factura Authorized sin ningún asiento
    // (log-and-continue), exactamente el hallazgo real (facturas 001-001-000000001/000000002 en
    // BD dev sin JournalEntry InvoiceIssued). Ahora SalesInvoiceAuthorizedPostingTranslator lanza
    // SalesInvoicePostingFailedException, que ErpDbContext.SaveChangesAsync propaga y revierte —
    // la factura permanece en Draft (rollback completo de esa transacción), sin asiento y sin
    // quedar autorizada silenciosamente.
    [Fact]
    public async Task Fallo_de_Posting_revierte_la_autorizacion_completa()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — fuerza RULE_NOT_FOUND dentro del pipeline.

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000002");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy);
        var act = async () => await db.SaveChangesAsync();

        await act.Should()
            .ThrowAsync<
                ERP.Application.Modules.Sales.Exceptions.SalesInvoicePostingFailedException
            >(
                because: "el asiento Sales/InvoiceIssued es obligatorio — un fallo del Posting "
                    + "Engine debe revertir la autorización completa, nunca dejarla a medias"
            );

        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.SalesInvoices.FirstAsync(x => x.Id == inv.Id);
        persisted.Status.Should().Be(ERP.Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);

        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x =>
            x.SourceEventId == inv.Id
        );
        entry.Should().BeNull();
    }

    [Fact]
    public async Task Republicar_el_mismo_evento_es_idempotente_un_solo_JournalEntry()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000003");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy);
        await db.SaveChangesAsync();

        // Republicación manual del mismo evento — nunca se re-autoriza la factura (Authorize()
        // es de un solo uso), se simula un reintento de entrega del mismo Domain Event.
        var repeated = new SalesInvoiceAuthorizedEvent(
            inv.Id,
            inv.InvoiceNumber,
            inv.AuthorizedGrandTotal!.Value,
            _createdBy,
            _cashSessionId,
            _tenantId,
            _companyId,
            issueDate,
            inv.Subtotal,
            inv.TotalVat,
            inv.TotalIce,
            inv.TotalDiscount
        );
        // ZH-SALES-CASH-CONCURRENCY-HARDENING-01 — la re-entrega se procesa como en producción:
        // dentro de una transacción y con SaveChanges real, de modo que cualquier efecto duplicado
        // que un handler stageara quedaría persistido (antes el test nunca guardaba).
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await publisher.Publish(repeated, CancellationToken.None);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id);
        count
            .Should()
            .Be(
                1,
                because: "el Posting Engine ya garantiza idempotencia por SourceEventId (Fase 3.1)"
            );
        (await verifyDb.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == inv.Id))
            .Should()
            .Be(1, because: "Caja es idempotente por origen: una factura, a lo sumo un SaleIncome");
        (await SessionBalanceAsync(verifyDb)).Should().Be(100m, because: "ningún efectivo duplicado");
    }

    [Fact]
    public async Task Dos_publicaciones_concurrentes_del_mismo_evento_no_producen_conflicto_de_concurrencia()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (seedDb, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(seedDb, issueDate);

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000004");
        seedDb.SalesInvoices.Add(inv);
        await seedDb.SaveChangesAsync();

        inv.Authorize(_createdBy);
        await seedDb.SaveChangesAsync();

        var evt = new SalesInvoiceAuthorizedEvent(
            inv.Id,
            inv.InvoiceNumber,
            inv.AuthorizedGrandTotal!.Value,
            _createdBy,
            _cashSessionId,
            _tenantId,
            _companyId,
            issueDate,
            inv.Subtotal,
            inv.TotalVat,
            inv.TotalIce,
            inv.TotalDiscount,
            // ZH-SALES-CONCURRENCY-POSTING-AUDIT-01 — sin efectivo físico: la redistribución solo
            // ejercita la idempotencia del Posting (advisory lock por SourceEventId), que es lo que
            // este test protege. Con efectivo, el handler de Caja (no idempotente ante re-entrega,
            // que producción no hace: el evento se publica una sola vez, dentro de la transacción
            // que lo origina) registraría un segundo SaleIncome o chocaría — correctamente — con el
            // xmin de la CashSession: ese era el origen del fallo intermitente.
            physicalCashApplied: 0m
        );

        // Dos redistribuciones concurrentes del mismo evento — cada una en su propio
        // ErpDbContext/transacción (simula el escenario real: Caja y Accounting reaccionan al
        // mismo evento; con dos publicaciones concurrentes, además se ejercita el advisory lock
        // de idempotencia entre las dos transacciones).
        //
        // Fase 5.5.1: la transacción se abre explícitamente ANTES de publisher.Publish() —
        // replica el orden real de producción (ErpDbContext.SaveChangesAsync ya tiene su
        // transacción abierta antes de publicar el Domain Event). Sin ella,
        // AcquireIdempotencyLockAsync corre pg_advisory_xact_lock en un statement autocommit
        // propio que se libera de inmediato, y el "advisory lock" deja de serializar nada —
        // exactamente la causa raíz investigada en la Fase 5.5.1.
        var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task RunAsync() =>
            Task.Run(async () =>
            {
                await go.Task.ConfigureAwait(false);
                var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
                await using var tx = await db.Database.BeginTransactionAsync();
                await publisher.Publish(evt, CancellationToken.None);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            });

        var taskA = RunAsync();
        var taskB = RunAsync();
        go.SetResult(true);

        var act = async () => await Task.WhenAll(taskA, taskB);
        await act.Should()
            .NotThrowAsync(
                because: "el advisory lock debe serializar las dos publicaciones concurrentes sin "
                    + "DbUpdateConcurrencyException ni violación UNIQUE — ambas ejecuciones del Posting completan sin error"
            );

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id);
        count
            .Should()
            .Be(
                1,
                because: "un único JournalEntry, sin importar cuántas veces se redistribuya el mismo evento concurrentemente"
            );
        (await verifyDb.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == inv.Id))
            .Should()
            .Be(1, because: "solo la autorización real registra efectivo; la redistribución no toca la caja");
    }

    // ── COMPRAS-METODO-ZH-01A2: Kardex sequence retry keeps domain events exactly once ─────────
    [Fact]
    public async Task Kardex_sequence_retry_authorizes_sale_with_every_effect_exactly_once()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var productId = Guid.NewGuid();
        Guid warehouseId;
        await using (var seed = CreateContext())
        {
            var wh = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(_tenantId, _branchId, "Bodega R", "BR",
                null, null, null, null, null, null, null, null, null, _createdBy, _companyId);
            seed.Set<ERP.Domain.Modules.Inventory.Entities.Warehouse>().Add(wh);
            await seed.SaveChangesAsync();
            warehouseId = wh.Id;
            var seedRepo = RetryStockRepository(seed);
            await seedRepo.AppendMovementAsync(_tenantId, _companyId, productId, warehouseId,
                ERP.Domain.Modules.Inventory.Enums.StockMovementType.PositiveAdjust, 100m, "UNIT", issueDate,
                "Stock inicial", null, null, _createdBy, unitCost: 1m);
            await seedRepo.SaveChangesWithSequenceRetryAsync();
        }

        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, issueDate);
        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000301");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();
        var stock = RetryStockRepository(db);
        await stock.AppendMovementAsync(_tenantId, _companyId, productId, warehouseId,
            ERP.Domain.Modules.Inventory.Enums.StockMovementType.SaleExit, -1m, "UNIT", issueDate,
            inv.InvoiceNumber, inv.Id, "SalesInvoice", _createdBy);
        inv.Authorize(_createdBy);

        // Another sale of the same product/warehouse commits first: our first save collides.
        await using (var other = CreateContext())
        {
            var otherRepo = RetryStockRepository(other);
            await otherRepo.AppendMovementAsync(_tenantId, _companyId, productId, warehouseId,
                ERP.Domain.Modules.Inventory.Enums.StockMovementType.SaleExit, -1m, "UNIT", issueDate,
                "otra venta", null, null, _createdBy);
            await otherRepo.SaveChangesWithSequenceRetryAsync();
        }

        await stock.SaveChangesWithSequenceRetryAsync();

        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(x => x.Id == inv.Id)).Status.ToString()
            .Should().Be("Authorized");
        var entryTypes = await verify.JournalEntries.Where(x => x.SourceEventId == inv.Id)
            .Select(x => x.SourceEventType).ToListAsync();
        entryTypes.Should().Contain("InvoiceIssued").And.OnlyHaveUniqueItems();
        (await verify.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == inv.Id)).Should().Be(1);
        var movements = await verify.Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>()
            .Where(m => m.ProductId == productId).OrderBy(m => m.SequenceNumber).ToListAsync();
        movements.Select(m => m.SequenceNumber).Should().Equal(1L, 2L, 3L);
        movements.Should().ContainSingle(m => m.SourceDocId == inv.Id).Which.SequenceNumber.Should().Be(3L);
        (await verify.OutboxMessages.CountAsync(m => m.EventName.Contains("SalesInvoiceAuthorized")
            && m.Payload.Contains(inv.Id.ToString()))).Should().Be(1);
    }

    private ERP.Infrastructure.Persistence.Repositories.Inventory.StockRepository RetryStockRepository(ErpDbContext db) =>
        new(db, new FixedCurrentCompany(_companyId), new ERP.Infrastructure.Persistence.PostgresDatabaseExceptionTranslator(),
            ERP.Infrastructure.Tests.TestData.StandardPrecisionPolicyProvider.Instance);

    // ── ZH-SALES-CASH-CONCURRENCY-HARDENING-01 ───────────────────────────────────

    private async Task<decimal> SessionBalanceAsync(ErpDbContext db) =>
        (await db.CashSessions.AsNoTracking().Include(x => x.Movements).SingleAsync(x => x.Id == _cashSessionId)).CurrentBalance;

    private async Task<Guid> SeedWarehouseAsync()
    {
        await using var seed = CreateContext();
        var wh = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(_tenantId, _branchId, "Bodega", $"B{Random.Shared.Next(100, 999)}",
            null, null, null, null, null, null, null, null, null, _createdBy, _companyId);
        seed.Set<ERP.Domain.Modules.Inventory.Entities.Warehouse>().Add(wh);
        await seed.SaveChangesAsync();
        return wh.Id;
    }

    /// <summary>
    /// Antes del lock, la autorización cargaba la CashSession al validar el pago en efectivo y el
    /// handler de Caja la reutilizaba tal cual: si otro flujo registraba un movimiento en medio, el
    /// xmin obsoleto hacía fallar la venta (409). Ahora el handler la bloquea y recarga (FOR UPDATE):
    /// la venta completa con el saldo vigente.
    /// </summary>
    [Fact]
    public async Task Sesion_obsoleta_en_el_contexto_se_bloquea_y_recarga_sin_conflicto()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var warehouseId = await SeedWarehouseAsync();
        var productId = Guid.NewGuid();

        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, issueDate);
        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000098");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        // Igual que AuthorizeSalesInvoiceHandler: la sesión queda trackeada antes de guardar…
        _ = await db.CashSessions.SingleAsync(x => x.Id == _cashSessionId);
        {
            // …y otro escritor (contexto con el interceptor oficial, ADR-020) la modifica en medio.
            var (other, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
            var s2 = await other.CashSessions.SingleAsync(x => x.Id == _cashSessionId);
            s2.RecordMovement(ERP.Domain.Modules.Caja.Enums.CashMovementType.SaleIncome, 5m, "otra venta", _createdBy);
            await other.SaveChangesAsync();
        }

        var stock = RetryStockRepository(db);
        await stock.AppendMovementAsync(_tenantId, _companyId, productId, warehouseId,
            ERP.Domain.Modules.Inventory.Enums.StockMovementType.PositiveAdjust, 10m, "UNIT", issueDate,
            inv.InvoiceNumber, inv.Id, "SalesInvoice", _createdBy, unitCost: 1m);
        inv.Authorize(_createdBy);

        await stock.SaveChangesWithSequenceRetryAsync();

        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(x => x.Id == inv.Id)).Status.ToString().Should().Be("Authorized");
        (await verify.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id)).Should().Be(1);
        (await verify.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == inv.Id)).Should().Be(1);
        (await verify.Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>().CountAsync(m => m.SourceDocId == inv.Id)).Should().Be(1);
        (await SessionBalanceAsync(verify)).Should().Be(105m, because: "5 del otro flujo + 100 de esta venta");
    }

    /// <summary>
    /// D — un fallo del posting ocurre DESPUÉS de publicar: el reintento de Kardex no lo repite
    /// (LastSaveFailureIsRetryable) y la venta se revierte entera — Draft, sin Kardex, asiento,
    /// movimiento de caja ni fila de outbox (sin estado parcial).
    /// </summary>
    [Fact]
    public async Task Fallo_de_posting_tras_publicar_revierte_Kardex_caja_y_outbox()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var warehouseId = await SeedWarehouseAsync();
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — RULE_NOT_FOUND dentro del pipeline, después del lock de Caja.
        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000097");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        var stock = RetryStockRepository(db);
        await stock.AppendMovementAsync(_tenantId, _companyId, Guid.NewGuid(), warehouseId,
            ERP.Domain.Modules.Inventory.Enums.StockMovementType.PositiveAdjust, 10m, "UNIT", issueDate,
            inv.InvoiceNumber, inv.Id, "SalesInvoice", _createdBy, unitCost: 1m);
        inv.Authorize(_createdBy);

        var act = async () => await stock.SaveChangesWithSequenceRetryAsync();
        await act.Should().ThrowAsync<ERP.Application.Modules.Sales.Exceptions.SalesInvoicePostingFailedException>();

        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(x => x.Id == inv.Id)).Status.ToString().Should().Be("Draft");
        (await verify.Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>().CountAsync(m => m.SourceDocId == inv.Id)).Should().Be(0);
        (await verify.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id)).Should().Be(0);
        (await verify.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == inv.Id)).Should().Be(0);
        (await verify.OutboxMessages.CountAsync(m => m.Payload.Contains(inv.Id.ToString()))).Should().Be(0);
        (await SessionBalanceAsync(verify)).Should().Be(0m);
    }

    private async Task<SalesInvoice> LoadInvoiceAsync(ErpDbContext db, Guid id) =>
        (await new ERP.Infrastructure.Persistence.Repositories.Sales.SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId))
            .GetByIdAsync(_tenantId, id))!;

    /// <summary>
    /// A — dos ventas DISTINTAS autorizadas a la vez sobre la misma CashSession (cada una en su
    /// propio contexto/transacción): el FOR UPDATE las serializa — ambas completan, cada una con su
    /// asiento y su SaleIncome, y el saldo final es la suma exacta. Varias rondas.
    /// </summary>
    [Fact]
    public async Task Dos_ventas_distintas_concurrentes_en_la_misma_sesion_se_serializan()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (seedDb, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(seedDb, issueDate);
        const int rounds = 5;
        var invoiceIds = new List<Guid>();

        for (var round = 0; round < rounds; round++)
        {
            var ids = new Guid[2];
            await using (var draftDb = CreateContext())
            {
                for (var k = 0; k < 2; k++)
                {
                    var draft = BuildAuthorizableInvoice(issueDate, $"001-002-{round:D4}{k:D5}");
                    draftDb.SalesInvoices.Add(draft);
                    ids[k] = draft.Id;
                }
                await draftDb.SaveChangesAsync();
            }

            var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task AuthorizeAsync(Guid id) =>
                Task.Run(async () =>
                {
                    var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
                    var inv = await LoadInvoiceAsync(db, id);
                    // Igual que la autorización real: la sesión ya está trackeada antes de guardar.
                    _ = await db.CashSessions.SingleAsync(x => x.Id == _cashSessionId);
                    await go.Task.ConfigureAwait(false);
                    inv.Authorize(_createdBy);
                    await db.SaveChangesAsync();
                });

            var tasks = ids.Select(AuthorizeAsync).ToArray();
            go.SetResult(true);
            await FluentActions.Awaiting(() => Task.WhenAll(tasks)).Should().NotThrowAsync(
                because: $"ronda {round}: la sesión se bloquea y recarga, no hay conflicto por xmin");
            invoiceIds.AddRange(ids);
        }

        await using var verify = CreateContext();
        foreach (var id in invoiceIds)
        {
            (await verify.JournalEntries.CountAsync(x => x.SourceEventId == id)).Should().Be(1);
            (await verify.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == id)).Should().Be(1);
        }
        (await SessionBalanceAsync(verify)).Should().Be(100m * 2 * rounds);
    }

    /// <summary>B — la misma autorización re-entregada dos veces a la vez, con efectivo: un asiento y un SaleIncome.</summary>
    [Fact]
    public async Task Republicacion_concurrente_con_efectivo_registra_un_solo_movimiento()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (seedDb, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(seedDb, issueDate);
        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000096");
        seedDb.SalesInvoices.Add(inv);
        await seedDb.SaveChangesAsync();
        inv.Authorize(_createdBy);
        await seedDb.SaveChangesAsync();

        var evt = new SalesInvoiceAuthorizedEvent(inv.Id, inv.InvoiceNumber, inv.AuthorizedGrandTotal!.Value,
            _createdBy, _cashSessionId, _tenantId, _companyId, issueDate, inv.Subtotal, inv.TotalVat, inv.TotalIce,
            inv.TotalDiscount, physicalCashApplied: 100m);

        var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task RedeliverAsync() =>
            Task.Run(async () =>
            {
                await go.Task.ConfigureAwait(false);
                var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
                await using var tx = await db.Database.BeginTransactionAsync();
                await publisher.Publish(evt, CancellationToken.None);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            });
        var a = RedeliverAsync();
        var b = RedeliverAsync();
        go.SetResult(true);
        await FluentActions.Awaiting(() => Task.WhenAll(a, b)).Should().NotThrowAsync();

        await using var verify = CreateContext();
        (await verify.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id)).Should().Be(1);
        (await verify.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == inv.Id)).Should().Be(1);
        (await SessionBalanceAsync(verify)).Should().Be(100m);
    }

    /// <summary>C — la misma factura autorizada a la vez desde dos contextos: gana una sola (xmin de SalesInvoice).</summary>
    [Fact]
    public async Task Doble_autorizacion_concurrente_de_la_misma_factura_solo_una_gana()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (seedDb, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(seedDb, issueDate);
        var draft = BuildAuthorizableInvoice(issueDate, "001-001-000000095");
        seedDb.SalesInvoices.Add(draft);
        await seedDb.SaveChangesAsync();

        var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Exception?> AuthorizeAsync() =>
            Task.Run(async () =>
            {
                var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
                var inv = await LoadInvoiceAsync(db, draft.Id);
                await go.Task.ConfigureAwait(false);
                try
                {
                    inv.Authorize(_createdBy);
                    await db.SaveChangesAsync();
                    return (Exception?)null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            });
        var a = AuthorizeAsync();
        var b = AuthorizeAsync();
        go.SetResult(true);
        var outcomes = await Task.WhenAll(a, b);

        outcomes.Count(e => e is null).Should().Be(1, because: "una sola transición Draft → Authorized");
        outcomes.Single(e => e is not null).Should().BeOfType<DbUpdateConcurrencyException>();

        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status.ToString().Should().Be("Authorized");
        (await verify.JournalEntries.CountAsync(x => x.SourceEventId == draft.Id)).Should().Be(1);
        (await verify.Set<ERP.Domain.Modules.Caja.Entities.CashMovement>().CountAsync(m => m.ReferenceId == draft.Id)).Should().Be(1);
        (await verify.OutboxMessages.CountAsync(m => m.Payload.Contains(draft.Id.ToString()))).Should().Be(1);
        (await SessionBalanceAsync(verify)).Should().Be(100m);
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
