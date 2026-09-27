using ERP.Application.Audit;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Tests.TestData;
using Moq;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el segundo consumidor real
/// del Posting Engine (Fase 3.4, ADR-026 §8): PurchaseInvoice.Confirm() → PurchaseInvoiceConfirmedEvent
/// → PurchaseInvoiceConfirmedPostingTranslator → IPostingEngine → JournalEntry Draft. Replica
/// exactamente el patrón de <see cref="SalesInvoiceAuthorizedPostingIntegrationTests"/> (Fase 3.3).
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseInvoiceConfirmedPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchases_posting_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _warehouseId;
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
        var supplier = BusinessPartner.Create(
            tenant.Id,
            TaxIdentification.SriRuc,
            "1791352688001",
            2,
            "Proveedor Test",
            _createdBy
        );
        var paymentTerm = PaymentTerm.Create(
            tenant.Id,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _createdBy
        );
        var warehouse = Warehouse.Create(
            tenant.Id,
            branch.Id,
            "Bodega Principal",
            "BOD-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _createdBy,
            company.Id,
            isMain: true
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.PaymentTerms.Add(paymentTerm);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;
        _warehouseId = warehouse.Id;
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
    /// escaneo de ensamblado) apuntando siempre a la misma instancia de ErpDbContext, para que el
    /// Translator opere dentro de la misma transacción/SaveChanges que confirmó la compra.</summary>
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
            // Mismo interceptor que registra la DI de producción (ADR-020, FROZEN) — necesario
            // para que un hijo nuevo agregado dentro de un domain event handler no quede mal
            // clasificado por el ChangeTracker. No relacionado con Posting.
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
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        // PurchaseInvoiceAuditHandler (Entity Audit, ADR-022) también escucha
        // PurchaseInvoiceConfirmedEvent — el escaneo de ensamblado de AddMediatR lo descubre
        // igual que al Translator, así que requiere su propia cadena de dependencias registrada
        // aquí para no romper el fan-out real de producción. No es parte del alcance de Fase 3.4.
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => tenantId,
            () => companyId,
            Guid.NewGuid()
        ));
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(PurchaseInvoiceConfirmedPostingTranslator).Assembly
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
        // Debit Subtotal == Credit GrandTotal (ambos 100) — balanceado con 2 líneas.
        var debitAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]),
            "Compras",
            null,
            AccountType.Expense,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var creditAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]),
            "Cuentas por pagar",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(debitAccount, creditAccount);

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Purchases",
            "InvoiceReceived",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(creditAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

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

    /// <summary>
    /// FLOW-READY-02F.2 — misma regla que <see cref="SeedRuleAndPeriodAsync"/>, con una tercera
    /// línea (Débito, TaxIrbpnr) para que el asiento balancee cuando la factura incluye IRBPNR:
    /// Debit(Subtotal + TaxIrbpnr) == Credit(GrandTotal), ya que GrandTotal = Subtotal + IRBPNR en
    /// esta factura de prueba (sin IVA/ICE, igual que el resto de fixtures de este archivo).
    /// </summary>
    private async Task SeedRuleAndPeriodWithIrbpnrAsync(ErpDbContext db, DateOnly entryDate)
    {
        var debitAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]),
            "Compras",
            null,
            AccountType.Expense,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var irbpnrAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"5.2.{Guid.NewGuid():N}"[..8]),
            "IRBPNR por pagar",
            null,
            AccountType.Expense,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var creditAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]),
            "Cuentas por pagar",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(debitAccount, irbpnrAccount, creditAccount);

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Purchases",
            "InvoiceReceived",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(irbpnrAccount.Id, AccountNature.Debit, PostingAmountKind.TaxIrbpnr);
        rule.AddLine(creditAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

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

    private PurchaseInvoice BuildConfirmableInvoiceWithIrbpnr(
        DateOnly issueDate,
        string invoiceNumber,
        decimal irbpnrAmount
    )
    {
        var inv = BuildConfirmableInvoice(issueDate, invoiceNumber);
        var line = inv.Lines[0];
        line.ReplaceTaxes(
            [
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    _tenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.02m,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    irbpnrAmount,
                    ERP.Domain.Modules.Purchases.Enums.PurchaseTaxSource.Xml
                ),
            ]
        );
        return inv;
    }

    private PurchaseInvoice BuildConfirmableInvoice(DateOnly issueDate, string invoiceNumber)
    {
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
            docTypeCode: "01",
            invoiceNumber: invoiceNumber,
            issueDate: issueDate,
            createdBy: _createdBy,
            paymentTermId: _paymentTermId,
            paymentTermName: "Contado",
            paymentTermInstallments: 1,
            paymentTermDaysBetween: 0,
            globalWarehouseId: _warehouseId
        );

        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _createdBy);

        return inv;
    }

    [Fact]
    public async Task Confirmar_PurchaseInvoice_genera_JournalEntry_Posted()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, issueDate);

        var inv = BuildConfirmableInvoice(issueDate, "001-001-000000001");
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Confirm(_createdBy);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x =>
            x.SourceEventId == inv.Id
        );

        entry.Should().NotBeNull();
        // Fase 5.2: el Posting Engine publica (Post()) el asiento antes de persistirlo.
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        entry.PostedAtUtc.Should().NotBeNull();
        entry.SourceModule.Should().Be("Purchases");
        entry.SourceEventType.Should().Be("InvoiceReceived");
    }

    [Fact]
    public async Task Confirmar_PurchaseInvoice_con_IRBPNR_genera_JournalEntry_balanceado()
    {
        // FLOW-READY-02F.2 — con una PostingRuleLine para TaxIrbpnr configurada, una compra con
        // IRBPNR confirma y el asiento resultante balancea: Debit(Subtotal + TaxIrbpnr) ==
        // Credit(GrandTotal), porque GrandTotal = Subtotal + IRBPNR (esta factura no aplica IVA/ICE).
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodWithIrbpnrAsync(db, issueDate);

        var inv = BuildConfirmableInvoiceWithIrbpnr(issueDate, "001-001-000000005", 0.48m);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.GrandTotal.Should().Be(100m + 0.48m);

        inv.Confirm(_createdBy);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(3);
        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);
        totalDebit.Should().Be(totalCredit, "el asiento debe balancear con la línea IRBPNR incluida");
        totalCredit.Should().Be(inv.GrandTotal);
    }

    /// <summary>
    /// Smoke funcional (checkpoint ADR-032 hasta Fase 6, 2026-08-29) — regla con las 4 líneas
    /// reales de un asiento de compra completo: Debit(Subtotal, TaxVat, TaxIce, TaxIrbpnr),
    /// Credit(GrandTotal). Verifica que Fase 3 (ICE ahora computado desde _taxes, ya no un
    /// escalar) no rompió el balance del asiento real contra Postgres.
    /// </summary>
    private async Task SeedRuleAndPeriodWithIvaIceIrbpnrAsync(ErpDbContext db, DateOnly entryDate)
    {
        var debitAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]),
            "Compras",
            null,
            AccountType.Expense,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var vatAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]),
            "IVA Crédito Tributario",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var iceAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"5.2.{Guid.NewGuid():N}"[..8]),
            "ICE (gasto)",
            null,
            AccountType.Expense,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var irbpnrAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"5.3.{Guid.NewGuid():N}"[..8]),
            "IRBPNR (gasto)",
            null,
            AccountType.Expense,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var creditAccount = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]),
            "Cuentas por pagar",
            null,
            AccountType.Liability,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        db.Accounts.AddRange(debitAccount, vatAccount, iceAccount, irbpnrAccount, creditAccount);

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Purchases",
            "InvoiceReceived",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(vatAccount.Id, AccountNature.Debit, PostingAmountKind.TaxVat);
        rule.AddLine(iceAccount.Id, AccountNature.Debit, PostingAmountKind.TaxIce);
        rule.AddLine(irbpnrAccount.Id, AccountNature.Debit, PostingAmountKind.TaxIrbpnr);
        rule.AddLine(creditAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

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

    private PurchaseInvoice BuildConfirmableInvoiceWithIvaIceIrbpnr(
        DateOnly issueDate,
        string invoiceNumber
    )
    {
        var inv = BuildConfirmableInvoice(issueDate, invoiceNumber);
        var line = inv.Lines[0];

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — ReplaceTaxes (Compras) reemplaza TODA la
        // colección: debe llamarse ANTES de ApplyTaxes, que re-sincroniza IVA/ICE sin tocar otras
        // filas (mismo orden ya corregido en Fase 5D-1/producción, ReceptionTaxHelper).
        line.ReplaceTaxes(
            [
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    _tenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.10m,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    0.30m,
                    ERP.Domain.Modules.Purchases.Enums.PurchaseTaxSource.Xml
                ),
            ]
        );
        // IVA 15% + ICE 10% (Percentage) — Fase 3 completada: IceCode/IceRate/IceAmount ahora
        // computados desde _taxes, nunca un escalar paralelo.
        line.ApplyTaxes("10", 15m, "IVA", "3072", 10m, "ICE bebidas");

        return inv;
    }

    [Fact]
    public async Task Smoke_IVA_ICE_IRBPNR_juntos_genera_asiento_balanceado_con_totales_correctos()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodWithIvaIceIrbpnrAsync(db, issueDate);

        var inv = BuildConfirmableInvoiceWithIvaIceIrbpnr(issueDate, "001-001-000000006");
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();

        // Subtotal 100, ICE 10 (10% de 100), IVA 15% sobre (100+10)=110 → 16.5, IRBPNR fijo 0.30.
        inv.TotalIce.Should().Be(10m);
        inv.Subtotal.Should().Be(100m);
        var line = inv.Lines[0];
        line.VatAmount.Should().Be(16.5m);
        line.IceCode.Should().Be("3072");
        line.IceAmount.Should().Be(10m);
        line.IrbpnrAmount.Should().Be(0.30m);

        inv.Confirm(_createdBy);
        await db.SaveChangesAsync();

        inv.ConfirmedGrandTotal.Should().Be(100m + 16.5m + 10m + 0.30m);

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(5, "Subtotal + TaxVat + TaxIce + TaxIrbpnr + GrandTotal");
        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);
        totalDebit.Should()
            .Be(
                totalCredit,
                "el asiento debe balancear con IVA+ICE+IRBPNR incluidos tras completar Fase 3"
            );
        totalCredit.Should().Be(inv.ConfirmedGrandTotal!.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirmation_cost_stock_payable_and_accounting_commit_or_rollback_together(bool postingConfigured)
    {
        var date = new DateOnly(2026, 9, 27);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await using var ownedDb = db;
        if (postingConfigured)
            await SeedRuleAndPeriodAsync(db, date);
        var invoice = BuildConfirmableInvoice(date, "001-001-000000099");
        invoice.ReplaceLines([
            PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Linea 1", 2m, 50m, "10", "UNIT"),
            PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Linea 2", 1m, 100m, "10", "UNIT")
        ], _createdBy);
        invoice.ApplyGlobalDiscount(10m, _createdBy);
        invoice.DistributeAdditionalCost(ERP.Domain.Modules.Purchases.Enums.PurchaseCostType.Freight,
            12m, [invoice.Lines[0].Id], _createdBy);
        invoice.DistributeAdditionalCost(ERP.Domain.Modules.Purchases.Enums.PurchaseCostType.OtherCost,
            3m, [invoice.Lines[1].Id], _createdBy);
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var productId = Guid.NewGuid();
        var precision = new Mock<ICompanyPrecisionPolicyProvider>();
        precision.Setup(p => p.GetEffectiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new EffectivePrecisionPolicyDto("Custom", 2, 4, 4, 2, 6, 6, 6, 0.01m, false, null, null,
                2, 2, 2, 2, 4, 2, 4));
        var stock = new StockRepository(db, new FixedCurrentCompany(_companyId),
            new PostgresDatabaseExceptionTranslator(), precision.Object);
        invoice.Confirm(_createdBy);
        foreach (var line in invoice.Lines)
            await stock.AppendMovementAsync(_tenantId, _companyId, productId, _warehouseId,
                StockMovementType.PurchaseEntry, line.QuantityInBaseUom, line.BaseUomCode,
                date, invoice.InvoiceNumber, invoice.Id, "PurchaseInvoice", _createdBy,
                unitCost: line.LandedUnitCost, sourceDocLineId: line.Id);
        var payables = new AccountsPayableService(new AccountsPayableRepository(db));
        await payables.StageFromOriginAsync(new CreateAccountsPayableFromOriginRequest(
            _tenantId, _companyId, _branchId, _supplierId, AccountsPayableOriginType.PurchaseInvoice,
            invoice.Id, "01", invoice.InvoiceNumber, date, date,
            [new AccountsPayableInstallmentInput(1, date, invoice.GrandTotal)]), _createdBy);

        if (postingConfigured)
            await stock.SaveChangesWithSequenceRetryAsync();
        else
        {
            var save = async () => await stock.SaveChangesWithSequenceRetryAsync();
            await save.Should().ThrowAsync<ERP.Application.Modules.Purchases.Exceptions.PurchasePostingFailedException>();
        }
        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.PurchaseInvoices.SingleAsync(x => x.Id == invoice.Id);
        var movements = await verifyDb.Set<StockMovement>().Where(m => m.SourceDocId == invoice.Id)
            .OrderBy(m => m.SequenceNumber).ToListAsync();
        var payable = await verifyDb.Set<AccountsPayable>().Include(p => p.Installments)
            .SingleOrDefaultAsync(p => p.OriginId == invoice.Id);
        var entry = await verifyDb.JournalEntries.Include(e => e.Lines)
            .SingleOrDefaultAsync(e => e.SourceEventId == invoice.Id);
        if (postingConfigured)
        {
            persisted.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
            movements.Select(m => m.RunningStockValue).Should().Equal(102m, 195m);
            movements.Select(m => m.SequenceNumber).Should().Equal(1L, 2L);
            movements.Select(m => m.SourceDocLineId).Should().Equal(invoice.Lines.Select(l => (Guid?)l.Id));
            payable!.TotalAmount.Should().Be(195m);
            entry!.Status.Should().Be(JournalEntryStatus.Posted);
            entry.Lines.Sum(l => l.Debit).Should().Be(195m);
            entry.Lines.Sum(l => l.Credit).Should().Be(payable.TotalAmount);
        }
        else
        {
            persisted.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
            movements.Should().BeEmpty();
            (await verifyDb.Set<CurrentStock>().AnyAsync(s => s.ProductId == productId)).Should().BeFalse();
            payable.Should().BeNull();
            entry.Should().BeNull();
        }
    }

    // ── COMPRAS-METODO-ZH-01A2: Kardex sequence retry keeps domain events exactly once ─────────

    private StockRepository NewStockRepository(ErpDbContext db) =>
        new(db, new FixedCurrentCompany(_companyId), new PostgresDatabaseExceptionTranslator(),
            ERP.Infrastructure.Tests.TestData.StandardPrecisionPolicyProvider.Instance);

    private async Task AppendCommittedMovementAsync(Guid productId, DateOnly date, decimal quantity, string reference)
    {
        await using var other = CreateContext();
        var repo = NewStockRepository(other);
        await repo.AppendMovementAsync(_tenantId, _companyId, productId, _warehouseId,
            StockMovementType.PositiveAdjust, quantity, "UNIT", date, reference, null, null, _createdBy,
            unitCost: 5m);
        await repo.SaveChangesWithSequenceRetryAsync();
    }

    private PurchaseInvoice StageConfirmation(DateOnly date, string number)
    {
        var invoice = BuildConfirmableInvoice(date, number);
        invoice.ReplaceLines([
            PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Linea 1", 2m, 50m, "10", "UNIT"),
            PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Linea 2", 1m, 100m, "10", "UNIT")
        ], _createdBy);
        invoice.ApplyGlobalDiscount(10m, _createdBy);
        invoice.DistributeAdditionalCost(ERP.Domain.Modules.Purchases.Enums.PurchaseCostType.Freight,
            12m, [invoice.Lines[0].Id], _createdBy);
        invoice.DistributeAdditionalCost(ERP.Domain.Modules.Purchases.Enums.PurchaseCostType.OtherCost,
            3m, [invoice.Lines[1].Id], _createdBy);
        return invoice;
    }

    // Same effects ConfirmPurchaseHandler tracks before its single SaveChangesWithSequenceRetryAsync.
    private async Task TrackConfirmationEffectsAsync(
        StockRepository stock, ErpDbContext db, PurchaseInvoice invoice, DateOnly date, Guid productId)
    {
        invoice.Confirm(_createdBy);
        foreach (var line in invoice.Lines)
            await stock.AppendMovementAsync(_tenantId, _companyId, productId, _warehouseId,
                StockMovementType.PurchaseEntry, line.QuantityInBaseUom, line.BaseUomCode,
                date, invoice.InvoiceNumber, invoice.Id, "PurchaseInvoice", _createdBy,
                unitCost: line.LandedUnitCost, sourceDocLineId: line.Id);
        await new AccountsPayableService(new AccountsPayableRepository(db)).StageFromOriginAsync(
            new CreateAccountsPayableFromOriginRequest(
                _tenantId, _companyId, _branchId, _supplierId, AccountsPayableOriginType.PurchaseInvoice,
                invoice.Id, "01", invoice.InvoiceNumber, date, date,
                [new AccountsPayableInstallmentInput(1, date, invoice.GrandTotal)]), _createdBy);
    }

    [Fact]
    public async Task Kardex_sequence_retry_confirms_purchase_with_every_effect_exactly_once()
    {
        var date = new DateOnly(2026, 9, 27);
        var productId = Guid.NewGuid();
        await AppendCommittedMovementAsync(productId, date, 10m, "Stock inicial");
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await using var ownedDb = db;
        await SeedRuleAndPeriodAsync(db, date);
        var invoice = StageConfirmation(date, "001-001-000000201");
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var stock = NewStockRepository(db);
        await TrackConfirmationEffectsAsync(stock, db, invoice, date, productId);

        // Another document commits the same product/warehouse first: our first save collides.
        await AppendCommittedMovementAsync(productId, date, 1m, "otro documento");
        await stock.SaveChangesWithSequenceRetryAsync();

        await using var verify = CreateContext();
        (await verify.PurchaseInvoices.SingleAsync(x => x.Id == invoice.Id)).Status
            .Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
        var movements = await verify.Set<StockMovement>().Where(m => m.ProductId == productId)
            .OrderBy(m => m.SequenceNumber).ToListAsync();
        movements.Select(m => m.SequenceNumber).Should().Equal(1L, 2L, 3L, 4L);
        var ours = movements.Where(m => m.SourceDocId == invoice.Id).ToList();
        ours.Select(m => m.SourceDocLineId).Should().Equal(invoice.Lines.Select(l => (Guid?)l.Id));
        ours.Select(m => m.RunningStockValue).Should().Equal(157m, 250m);
        (await verify.Set<CurrentStock>().SingleAsync(s => s.ProductId == productId)).Quantity.Should().Be(14m);
        var payables = await verify.Set<AccountsPayable>().Include(p => p.Installments)
            .Where(p => p.OriginId == invoice.Id).ToListAsync();
        payables.Should().ContainSingle().Which.TotalAmount.Should().Be(195m);
        var entries = await verify.JournalEntries.Include(e => e.Lines)
            .Where(e => e.SourceEventId == invoice.Id).ToListAsync();
        entries.Should().ContainSingle();
        entries[0].Status.Should().Be(JournalEntryStatus.Posted);
        entries[0].Lines.Sum(l => l.Debit).Should().Be(195m);
        entries[0].Lines.Sum(l => l.Credit).Should().Be(195m);
        (await verify.OutboxMessages.CountAsync(m => m.EventName.Contains("PurchaseInvoiceConfirmed")
            && m.Payload.Contains(invoice.Id.ToString()))).Should().Be(1);
    }

    [Fact]
    public async Task Failure_after_publishing_is_not_replayed_by_the_kardex_retry()
    {
        var date = new DateOnly(2026, 9, 27);
        var productId = Guid.NewGuid();
        var publisher = new ConflictAfterPublishPublisher();
        // Production tracking interceptor, so the first write succeeds and the failure is post-publish.
        await using var db = new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(_tenantId), publisher, new FixedCurrentCompany(_companyId));
        var invoice = StageConfirmation(date, "001-001-000000202");
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var stock = NewStockRepository(db);
        await TrackConfirmationEffectsAsync(stock, db, invoice, date, productId);

        var save = async () => await stock.SaveChangesWithSequenceRetryAsync();

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        publisher.ConfirmedPublications.Should().Be(1, "a half-applied unit of work must not be retried");
        await using var verify = CreateContext();
        (await verify.PurchaseInvoices.SingleAsync(x => x.Id == invoice.Id)).Status
            .Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
        (await verify.Set<StockMovement>().AnyAsync(m => m.ProductId == productId)).Should().BeFalse();
        (await verify.Set<AccountsPayable>().AnyAsync(p => p.OriginId == invoice.Id)).Should().BeFalse();
        (await verify.OutboxMessages.AnyAsync(m => m.EventName.Contains("PurchaseInvoiceConfirmed")
            && m.Payload.Contains(invoice.Id.ToString()))).Should().BeFalse();
    }

    /// <summary>Simulates a retryable conflict raised after the confirmation event was published.</summary>
    private sealed class ConflictAfterPublishPublisher : IPublisher
    {
        public int ConfirmedPublications { get; private set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            notification is INotification n ? Publish(n, cancellationToken) : Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (notification is not PurchaseInvoiceConfirmedEvent)
                return Task.CompletedTask;
            ConfirmedPublications++;
            throw new DbUpdateConcurrencyException("Simulated conflict after publishing.");
        }
    }

    [Fact]
    public async Task Fallo_de_Posting_revierte_la_confirmacion()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — fuerza RULE_NOT_FOUND dentro del pipeline.

        var inv = BuildConfirmableInvoice(issueDate, "001-001-000000002");
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Confirm(_createdBy);
        var act = async () => await db.SaveChangesAsync();

        await act.Should()
            .ThrowAsync<ERP.Application.Modules.Purchases.Exceptions.PurchasePostingFailedException>();

        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.PurchaseInvoices.FirstAsync(x => x.Id == inv.Id);
        persisted.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);

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

        var inv = BuildConfirmableInvoice(issueDate, "001-001-000000003");
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Confirm(_createdBy);
        await db.SaveChangesAsync();

        // Republicación manual del mismo evento — nunca se re-confirma la compra (Confirm() es de
        // un solo uso), se simula un reintento de entrega del mismo Domain Event.
        var repeated = new PurchaseInvoiceConfirmedEvent(
            _tenantId,
            inv.Id,
            _supplierId,
            inv.InvoiceNumber,
            inv.GrandTotal,
            _companyId,
            issueDate,
            inv.Subtotal,
            inv.Lines.Sum(l => l.TotalLineCost),
            inv.TotalVat,
            inv.TotalIce,
            inv.TotalDiscount
        );
        await publisher.Publish(repeated, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id);
        count
            .Should()
            .Be(
                1,
                because: "el Posting Engine ya garantiza idempotencia por SourceEventId (Fase 3.1)"
            );
    }

    [Fact]
    public async Task Dos_publicaciones_concurrentes_del_mismo_evento_no_producen_conflicto_de_concurrencia()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (seedDb, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(seedDb, issueDate);

        var inv = BuildConfirmableInvoice(issueDate, "001-001-000000004");
        seedDb.PurchaseInvoices.Add(inv);
        await seedDb.SaveChangesAsync();

        inv.Confirm(_createdBy);
        await seedDb.SaveChangesAsync();

        var evt = new PurchaseInvoiceConfirmedEvent(
            _tenantId,
            inv.Id,
            _supplierId,
            inv.InvoiceNumber,
            inv.GrandTotal,
            _companyId,
            issueDate,
            inv.Subtotal,
            inv.Lines.Sum(l => l.TotalLineCost),
            inv.TotalVat,
            inv.TotalIce,
            inv.TotalDiscount
        );

        // Dos redistribuciones concurrentes del mismo evento, cada una en su propio
        // ErpDbContext/transacción — ejercita el advisory lock de idempotencia entre ambas.
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
                    + "DbUpdateConcurrencyException ni violación UNIQUE"
            );

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id);
        count
            .Should()
            .Be(
                1,
                because: "un único JournalEntry, sin importar cuántas veces se redistribuya el mismo evento concurrentemente"
            );
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
