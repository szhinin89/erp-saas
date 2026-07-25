using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
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
using Xunit;

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
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, _createdBy,
            companyId: company.Id);
        var supplier = BusinessPartner.Create(
            tenant.Id, TaxIdentification.SriRuc, "1791352688001", PersonType.Legal, "Proveedor Test", _createdBy);
        var paymentTerm = PaymentTerm.Create(tenant.Id, "CONT", "Contado", installments: 1, daysBetweenInstallments: 0, _createdBy);
        var warehouse = Warehouse.Create(
            tenant.Id, branch.Id, "Bodega Principal", "BOD-01",
            null, null, null, null, null, null, null, null, null,
            _createdBy, company.Id, isMain: true);

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
            options, new FixedCurrentTenant(_tenantId), publisher ?? new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    /// <summary>Construye un contenedor DI real (mismo mecanismo de producción: AddMediatR con
    /// escaneo de ensamblado) apuntando siempre a la misma instancia de ErpDbContext, para que el
    /// Translator opere dentro de la misma transacción/SaveChanges que confirmó la compra.</summary>
    private static (ErpDbContext db, IPublisher publisher) BuildWiredContext(
        Guid tenantId, Guid companyId, PostgreSqlContainer postgres)
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            // Mismo interceptor que registra la DI de producción (ADR-020, FROZEN) — necesario
            // para que un hijo nuevo agregado dentro de un domain event handler no quede mal
            // clasificado por el ChangeTracker. No relacionado con Posting.
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
            .Options;
        var db = new ErpDbContext(options, new FixedCurrentTenant(tenantId), deferred, new FixedCurrentCompany(companyId));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        // PurchaseInvoiceAuditHandler (Entity Audit, ADR-022) también escucha
        // PurchaseInvoiceConfirmedEvent — el escaneo de ensamblado de AddMediatR lo descubre
        // igual que al Translator, así que requiere su propia cadena de dependencias registrada
        // aquí para no romper el fan-out real de producción. No es parte del alcance de Fase 3.4.
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(() => tenantId, () => companyId, Guid.NewGuid()));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PurchaseInvoiceConfirmedPostingTranslator).Assembly));

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
            _tenantId, _companyId, AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]), "Compras", null,
            AccountType.Expense, AccountNature.Debit, allowsPosting: true, createdBy: _createdBy);
        var creditAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]), "Cuentas por pagar", null,
            AccountType.Liability, AccountNature.Credit, allowsPosting: true, createdBy: _createdBy);
        db.Accounts.AddRange(debitAccount, creditAccount);

        var rule = PostingRule.Create(_tenantId, _companyId, "Purchases", "InvoiceReceived", null, null, null, _createdBy);
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(creditAccount.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);

        var period = AccountingPeriod.Create(
            _tenantId, _companyId, entryDate.Year, entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(entryDate.Year, entryDate.Month, DateTime.DaysInMonth(entryDate.Year, entryDate.Month)),
            _createdBy);

        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private PurchaseInvoice BuildConfirmableInvoice(DateOnly issueDate, string invoiceNumber)
    {
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId, _companyId, _branchId, _supplierId, "Proveedor Test", "1791352688001",
            docTypeCode: "01", invoiceNumber: invoiceNumber, issueDate: issueDate, createdBy: _createdBy,
            paymentTermId: _paymentTermId, paymentTermName: "Contado", paymentTermInstallments: 1, paymentTermDaysBetween: 0,
            globalWarehouseId: _warehouseId);

        var line = PurchaseInvoiceDetail.Create(
            inv.Id, _tenantId, "Producto Test", quantity: 1, unitPrice: 100m, vatCode: "10", uomCode: "UNIT");
        inv.ReplaceLines(new[] { line }, _createdBy);

        return inv;
    }

    [Fact]
    public async Task Confirmar_PurchaseInvoice_genera_JournalEntry_Draft()
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
        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Draft);
        entry.SourceModule.Should().Be("Purchases");
        entry.SourceEventType.Should().Be("InvoiceReceived");
    }

    [Fact]
    public async Task Fallo_de_Posting_no_revierte_la_confirmacion()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — fuerza RULE_NOT_FOUND dentro del pipeline.

        var inv = BuildConfirmableInvoice(issueDate, "001-001-000000002");
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Confirm(_createdBy);
        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync(because: "el fallo del Posting Engine no debe revertir la confirmación de la compra");

        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.PurchaseInvoices.FirstAsync(x => x.Id == inv.Id);
        persisted.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);

        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);
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
            _tenantId, inv.Id, _supplierId, inv.InvoiceNumber, inv.GrandTotal, _companyId, issueDate,
            inv.Subtotal, inv.TotalVat, inv.TotalIce, inv.TotalDiscount);
        await publisher.Publish(repeated, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id);
        count.Should().Be(1, because: "el Posting Engine ya garantiza idempotencia por SourceEventId (Fase 3.1)");
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
            _tenantId, inv.Id, _supplierId, inv.InvoiceNumber, inv.GrandTotal, _companyId, issueDate,
            inv.Subtotal, inv.TotalVat, inv.TotalIce, inv.TotalDiscount);

        // Dos redistribuciones concurrentes del mismo evento, cada una en su propio
        // ErpDbContext/transacción — ejercita el advisory lock de idempotencia entre ambas.
        var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task RunAsync() => Task.Run(async () =>
        {
            await go.Task.ConfigureAwait(false);
            var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
            await publisher.Publish(evt, CancellationToken.None);
            await db.SaveChangesAsync();
        });

        var taskA = RunAsync();
        var taskB = RunAsync();
        go.SetResult(true);

        var act = async () => await Task.WhenAll(taskA, taskB);
        await act.Should().NotThrowAsync(because: "el advisory lock debe serializar las dos publicaciones concurrentes sin " +
            "DbUpdateConcurrencyException ni violación UNIQUE");

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == inv.Id);
        count.Should().Be(1, because: "un único JournalEntry, sin importar cuántas veces se redistribuya el mismo evento concurrentemente");
    }

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Inner!.Publish(notification, cancellationToken);
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
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
