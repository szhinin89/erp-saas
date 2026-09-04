using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// RETENTIONS-POSTING-RULE-SEED-01H — casos 4 y 5 del plan de tests de esa fase: a diferencia de
/// <c>RetentionExpenseEndToEndTests</c> (que hasta esta fase creaba su propia PostingRule de
/// Retentions como fixture LOCAL del test), esta suite siembra la regla vía
/// <see cref="AccountingBootstrapStep"/> real — el mismo mecanismo que corre para toda Company
/// nueva del ERP — y confirma que <see cref="RetentionDocumentIssuedPostingTranslator"/> la
/// encuentra y postea un asiento balanceado usando exactamente esa configuración de seed, sin
/// ningún fixture de PostingRule adicional. PostgreSQL 16 real vía Testcontainers, mismo patrón
/// que <c>RetentionExpenseEndToEndTests</c>/<c>SupplierPaymentEndToEndTests</c>. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class RetentionsPostingRuleSeedIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_retention_postingrule_seed_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("RETSEED Tenant", $"retseed-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "RETSEED Empresa Retenedora S.A.",
            createdBy: _createdBy
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;

        // Seed real de producción — el mismo step que CompanyBootstrapOrchestrator corre para
        // toda Company nueva: Plan de Cuentas retail + AccountingPeriod + MinimalPostingRules
        // (incluye "Retentions"/"DocumentIssued" desde RETENTIONS-POSTING-RULE-SEED-01H). Sin
        // ningún fixture local de PostingRule.
        var bootstrapStep = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
        await bootstrapStep.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _createdBy));
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
    /// RetentionExpenseEndToEndTests.BuildWiredContext — confirma que
    /// RetentionDocumentIssuedPostingTranslator se registra automáticamente como
    /// INotificationHandler y resuelve IPostingEngine/PostingRuleRepository reales.</summary>
    private (ErpDbContext db, IPublisher publisher) BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
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
        services.AddScoped<ICompanyFinancialDestinationRepository, CompanyFinancialDestinationRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(RetentionDocumentIssuedPostingTranslator).Assembly)
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    [Fact]
    public async Task Translator_encuentra_la_regla_sembrada_y_postea_asiento_balanceado_con_cuentas_del_seed()
    {
        var (db, publisher) = BuildWiredContext();

        var retentionDocumentId = Guid.NewGuid();
        var evt = new RetentionDocumentIssuedEvent(
            _tenantId,
            retentionDocumentId,
            _companyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceDocumentId: Guid.NewGuid(),
            subjectBusinessPartnerId: Guid.NewGuid(),
            retentionNumber: "001-001-000000001",
            totalRetainedVat: 10.5m,
            totalRetainedIncome: 0m,
            totalRetained: 10.5m,
            issueDate: new DateOnly(DateTime.UtcNow.Year, 1, 15)
        );

        await publisher.Publish(evt);
        // IPostingEngine.PostAsync deja el JournalEntry en staging en el ChangeTracker (ver
        // remarks de PostingEngine.cs) — en producción, el flush final ocurre dentro del mismo
        // ErpDbContext.SaveChangesAsync que originó el evento; aquí lo replicamos explícitamente.
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.SourceModule == "Retentions" && x.SourceEventId == retentionDocumentId);

        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Should().HaveCount(2);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit), "el asiento debe quedar balanceado (Σdebe == Σhaber)");
        entry.Lines.Sum(l => l.Debit).Should().Be(10.5m);

        var accountsById = await verifyDb.Accounts
            .Where(a => a.CompanyId == _companyId)
            .ToDictionaryAsync(a => a.Id, a => a.Code.Value);

        var debitLine = entry.Lines.Single(l => l.Debit > 0);
        accountsById[debitLine.AccountId].Should().Be("2.1.01.001", "Debe = CxP proveedor, cuenta del plan sembrado, no un fixture local");

        var creditLine = entry.Lines.Single(l => l.Credit > 0);
        accountsById[creditLine.AccountId].Should().Be("2.1.02.002", "Haber = Retenciones IVA por pagar, cuenta del plan sembrado, no un fixture local");
    }

    /// <summary>
    /// RETENTIONS-POSTING-RULE-SEED-01H — regresión previa a esta fase: sin la PostingRule
    /// sembrada (empresa que por algún motivo nunca pasó por el bootstrap/backfill), el mismo
    /// evento falla fail-closed vía <c>RetentionPostingFailedException</c> — nunca contabiliza en
    /// silencio ni con una cuenta improvisada.
    /// </summary>
    [Fact]
    public async Task Sin_la_postingrule_sembrada_el_evento_falla_fail_closed()
    {
        await using (var setupDb = CreateContext())
        {
            var rule = await setupDb.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued"
            );
            setupDb.PostingRules.Remove(rule);
            await setupDb.SaveChangesAsync();
        }

        var (_, publisher) = BuildWiredContext();

        var evt = new RetentionDocumentIssuedEvent(
            _tenantId,
            Guid.NewGuid(),
            _companyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceDocumentId: Guid.NewGuid(),
            subjectBusinessPartnerId: Guid.NewGuid(),
            retentionNumber: "001-001-000000002",
            totalRetainedVat: 5m,
            totalRetainedIncome: 0m,
            totalRetained: 5m,
            issueDate: new DateOnly(DateTime.UtcNow.Year, 1, 15)
        );

        var act = async () => await publisher.Publish(evt);

        await act.Should().ThrowAsync<ERP.Application.Modules.Retentions.Exceptions.RetentionPostingFailedException>();
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
