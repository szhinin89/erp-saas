using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
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
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, _createdBy,
            companyId: company.Id);
        var customer = BusinessPartner.Create(tenant.Id, "05", "1710034065", PersonType.Natural, "Cliente Test", _createdBy);
        var establishment = Establishment.Create(
            tenant.Id, branchId: branch.Id, company.Id,
            code: "001", name: "Matriz Test", address: "Av. Principal 123",
            phone: null, isMain: true, createdBy: _createdBy);
        var cashRegister = CashRegister.Create(
            tenant.Id, company.Id, branch.Id, "CAJA-01", "Caja Principal", _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id, company.Id, establishment.Id,
            code: "001", name: "PE-001", emissionType: EmissionType.Electronic,
            isDefault: true, createdBy: _createdBy);
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id, company.Id, branch.Id, _createdBy,
            cashRegister.Id, "CAJA-01", "Caja Principal",
            emissionPoint.Id, "001",
            0m, _createdBy);
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
            options, new FixedCurrentTenant(_tenantId), publisher ?? new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    /// <summary>Construye un contenedor DI real (mismo mecanismo de producción: AddMediatR con
    /// escaneo de ensamblado) apuntando siempre a la misma instancia de ErpDbContext, para que
    /// el Translator y el handler de Caja operen dentro de la misma transacción/SaveChanges.</summary>
    private static (ErpDbContext db, IPublisher publisher) BuildWiredContext(
        Guid tenantId, Guid companyId, PostgreSqlContainer postgres)
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
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
            .Options;
        var db = new ErpDbContext(options, new FixedCurrentTenant(tenantId), deferred, new FixedCurrentCompany(companyId));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(companyId));
        services.AddScoped<ICashSessionRepository, ERP.Infrastructure.Persistence.Repositories.Caja.CashSessionRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SalesInvoiceAuthorizedPostingTranslator).Assembly));

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
            _tenantId, _companyId, AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]), "Caja", null,
            AccountType.Asset, AccountNature.Debit, allowsPosting: true, createdBy: _createdBy);
        var creditAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"4.1.{Guid.NewGuid():N}"[..8]), "Ventas", null,
            AccountType.Income, AccountNature.Credit, allowsPosting: true, createdBy: _createdBy);
        db.Accounts.AddRange(debitAccount, creditAccount);

        var rule = PostingRule.Create(_tenantId, _companyId, "Sales", "InvoiceIssued", null, null, null, _createdBy);
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(creditAccount.Id, AccountNature.Credit, PostingAmountKind.Subtotal);

        var period = AccountingPeriod.Create(
            _tenantId, _companyId, entryDate.Year, entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(entryDate.Year, entryDate.Month, DateTime.DaysInMonth(entryDate.Year, entryDate.Month)),
            _createdBy);

        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private SalesInvoice BuildAuthorizableInvoice(DateOnly issueDate, string invoiceNumber)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0);

        var inv = SalesInvoice.CreateDraft(
            _tenantId, _companyId, _branchId, _customerId, customer,
            invoiceNumber: invoiceNumber, issueDate: issueDate, createdBy: _createdBy,
            paymentTerm: paymentTerm, cashSessionId: _cashSessionId, emissionPointId: null);

        var line = SalesInvoiceDetail.Create(
            inv.Id, _tenantId, "Producto Test", quantity: 1, unitPrice: 100m, vatCode: "10", uomCode: "UNIT");
        inv.ReplaceLines(new[] { line }, _createdBy);

        // Sin ApplyTaxes() (fuera del alcance de este test — lo hace AuthorizeSalesInvoiceHandler
        // vía ISriTaxResolver), TaxInclusiveTotal = TaxableBase = unitPrice * quantity = 100.
        var payment = SalesInvoicePayment.Create(inv.Id, _tenantId, Guid.NewGuid(), "01", "Efectivo", 100m);
        inv.ReplacePayments(new[] { payment }, _createdBy);

        return inv;
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
        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);

        entry.Should().NotBeNull();
        // Fase 5.2: el Posting Engine publica (Post()) el asiento antes de persistirlo.
        entry!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        entry.PostedAtUtc.Should().NotBeNull();
        entry.SourceModule.Should().Be("Sales");
        entry.SourceEventType.Should().Be("InvoiceIssued");
    }

    [Fact]
    public async Task Fallo_de_Posting_no_revierte_la_autorizacion()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — fuerza RULE_NOT_FOUND dentro del pipeline.

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000002");
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy);
        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync(because: "el fallo del Posting Engine no debe revertir la autorización de la factura");

        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.SalesInvoices.FirstAsync(x => x.Id == inv.Id);
        persisted.Status.Should().Be(ERP.Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);

        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x => x.SourceEventId == inv.Id);
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
            inv.Id, inv.InvoiceNumber, inv.AuthorizedGrandTotal!.Value, _createdBy, _cashSessionId,
            _tenantId, _companyId, issueDate,
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

        var inv = BuildAuthorizableInvoice(issueDate, "001-001-000000004");
        seedDb.SalesInvoices.Add(inv);
        await seedDb.SaveChangesAsync();

        inv.Authorize(_createdBy);
        await seedDb.SaveChangesAsync();

        var evt = new SalesInvoiceAuthorizedEvent(
            inv.Id, inv.InvoiceNumber, inv.AuthorizedGrandTotal!.Value, _createdBy, _cashSessionId,
            _tenantId, _companyId, issueDate,
            inv.Subtotal, inv.TotalVat, inv.TotalIce, inv.TotalDiscount);

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

        Task RunAsync() => Task.Run(async () =>
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
        await act.Should().NotThrowAsync(because: "el advisory lock debe serializar las dos publicaciones concurrentes sin " +
            "DbUpdateConcurrencyException ni violación UNIQUE — Caja y Accounting deben completar ambas ejecuciones sin error");

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
