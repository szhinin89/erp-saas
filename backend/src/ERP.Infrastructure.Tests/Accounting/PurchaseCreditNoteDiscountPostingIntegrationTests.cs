using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
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
/// PURCHASE-CREDIT-NOTE-DISCOUNT-POSTING-ACCOUNT-01 — suite de integración (PostgreSQL 16 real vía
/// Testcontainers, mismo patrón que <see cref="PurchaseReturnAuthorizedPostingIntegrationTests"/>)
/// para el consumidor real de <see cref="PurchaseCreditNoteAuthorizedPostingTranslator"/> cuando la
/// NC es tipo Discount: verifica, contra el Plan de Cuentas/PostingRule REALES sembrados por
/// <see cref="AccountingBootstrapStep"/> (no una regla sintética de 3 cuentas), que el Haber de
/// Subtotal/TaxIce va a "4.2.01.002 Descuentos obtenidos en compras" — nunca a
/// "1.1.04.001 Inventario mercaderias" — y que ni PurchaseReturn ni StockMovement se crean. Requiere
/// Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseCreditNoteDiscountPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_credit_note_discount_posting_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private readonly Guid _createdBy = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenantId: _tenantId, name: "Matriz", address: "Av. Principal 123", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: true, createdBy: _createdBy, companyId: _companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branchId = branch.Id;

        var supplier = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Proveedor Test", _createdBy);
        db.BusinessPartners.Add(supplier);
        var paymentTerm = PaymentTerm.Create(_tenantId, "CONT", "Contado", installments: 1, daysBetweenInstallments: 0, _createdBy);
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;

        // Plan de cuentas + PostingRules REALES (incluida la corrección de este ticket) — no una
        // regla sintética: esta suite existe justamente para probar que el seed real usa la cuenta
        // correcta, nunca inventario.
        var bootstrap = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
        await bootstrap.ExecuteAsync(new ERP.Application.Common.Interfaces.CompanyBootstrapContext(_tenantId, _companyId, _createdBy));
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options, new FixedCurrentTenant(_tenantId), publisher ?? new NoOpPublisher(), new FixedCurrentCompany(_companyId)
        );
    }

    /// <summary>Mismo mecanismo que PurchaseReturnAuthorizedPostingIntegrationTests.BuildWiredContext.</summary>
    private (ErpDbContext db, IPublisher publisher) BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
            .Options;
        var db = new ErpDbContext(options, new FixedCurrentTenant(_tenantId), deferred, new FixedCurrentCompany(_companyId));

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
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(() => _tenantId, () => _companyId, Guid.NewGuid()));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PurchaseCreditNoteAuthorizedPostingTranslator).Assembly));

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();
        return (db, deferred);
    }

    private async Task EnsureCurrentYearPeriodAsync(ErpDbContext db)
    {
        var currentYear = DateTime.UtcNow.Year;
        var hasPeriod = await db.AccountingPeriods.AnyAsync(p => p.CompanyId == _companyId && p.FiscalYear == currentYear);
        if (!hasPeriod)
        {
            db.AccountingPeriods.Add(
                ERP.Domain.Modules.Accounting.Entities.AccountingPeriod.Create(
                    _tenantId, _companyId, currentYear, 1,
                    new DateOnly(currentYear, 1, 1), new DateOnly(currentYear, 12, 31), _createdBy
                )
            );
            await db.SaveChangesAsync();
        }
    }

    private async Task<(PurchaseInvoice Invoice, AccountsPayable Payable)> SeedConfirmedInvoiceAsync(
        ErpDbContext db, decimal lineTotal = 100m
    )
    {
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId, _companyId, _branchId, _supplierId, "Proveedor Test", "1791352688001",
            "01", $"001-001-{Random.Shared.Next(100000, 999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5), _createdBy,
            _paymentTermId, "Contado", 1, 30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id, _tenantId, "Producto con descuento", quantity: 1m, unitPrice: lineTotal,
            vatCode: "2", uomCode: "UNIT"
        );
        line.ApplyTaxes("2", 15m, "IVA", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _createdBy);
        inv.Confirm(_createdBy);

        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId, _companyId, _branchId, _supplierId, AccountsPayableOriginType.PurchaseInvoice,
            inv.Id, "01", inv.InvoiceNumber, inv.IssueDate, inv.IssueDate, _createdBy
        );
        payable.AddInstallment(1, inv.IssueDate.AddDays(30), lineTotal * 1.15m);

        db.PurchaseInvoices.Add(inv);
        db.Add(payable);
        await db.SaveChangesAsync();
        return (inv, payable);
    }

    [Fact]
    public async Task Autorizar_NC_descuento_acredita_Descuentos_obtenidos_en_compras_no_Inventario_y_balancea()
    {
        var (db, _) = BuildWiredContext();
        await EnsureCurrentYearPeriodAsync(db);
        var (inv, payable) = await SeedConfirmedInvoiceAsync(db, 100m);
        var summary = inv.TaxSummaries.Should().ContainSingle().Which;

        var creditNote = PurchaseCreditNote.CreateDraft(
            _tenantId, _companyId, _branchId, _supplierId, inv.Id, null,
            PurchaseCreditNoteApplicationType.Discount, "001-001-000000005", null, null, null,
            DateOnly.FromDateTime(DateTime.UtcNow), "Descuento por pronto pago",
            Array.Empty<PurchaseCreditNote.DraftLineInput>(),
            [new(summary.Id, summary.VatCode, summary.VatRate, summary.VatName, summary.IceCode,
                summary.IceRate, summary.IceName, 20m, summary.IrbpnrCode, summary.IrbpnrRate,
                summary.IrbpnrName, summary.TaxableBase, summary.IrbpnrAmount)],
            _createdBy, Guid.NewGuid(), "create-hash"
        );
        creditNote.Authorize(payable.OutstandingAmount, _createdBy, Guid.NewGuid(), "authorize-hash");

        db.PurchaseCreditNotes.Add(creditNote);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb.JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == creditNote.Id);
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        entry.SourceModule.Should().Be("Purchases");
        entry.SourceEventType.Should().Be("PurchaseCreditNoteAuthorized");

        // Asiento balanceado (§19.1bis).
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));

        var accountsById = await verifyDb.Accounts
            .Where(a => a.CompanyId == _companyId)
            .ToDictionaryAsync(a => a.Id, a => a.Code.Value);

        var creditedCodes = entry.Lines.Where(l => l.Credit > 0).Select(l => accountsById[l.AccountId]).ToList();
        creditedCodes.Should().Contain("4.2.01.002", "el descuento debe acreditar la cuenta de descuentos obtenidos en compras");
        creditedCodes.Should().NotContain("1.1.04.001", "una NC por descuento nunca debe tocar Inventario mercaderias");

        var debitedCodes = entry.Lines.Where(l => l.Debit > 0).Select(l => accountsById[l.AccountId]).ToList();
        debitedCodes.Should().Contain("2.1.01.001", "reduce CxP proveedores");

        // No crea PurchaseReturn ni mueve Kardex.
        (await verifyDb.PurchaseReturns.CountAsync(r => r.PurchaseInvoiceId == inv.Id)).Should().Be(0);
        (await verifyDb.Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>()
            .CountAsync(m => m.SourceDocId == creditNote.Id)).Should().Be(0);
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

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
