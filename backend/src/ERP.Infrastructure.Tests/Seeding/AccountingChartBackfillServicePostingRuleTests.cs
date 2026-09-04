using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// RETENTIONS-TAX-COMPONENT-POSTING-02C — cubre el punto más delicado de la fase: detectar y
/// corregir, vía <see cref="AccountingChartBackfillService.EnsureAsync"/>, una company activa cuya
/// PostingRule "Retentions"/"DocumentIssued" YA EXISTE (por eso el chequeo por clave de
/// RETENTIONS-POSTING-RULE-SEED-01H la pasaba) pero con menos líneas de las que la forma vigente
/// declara hoy — el mismo tipo de bug que 01H corrigió un nivel más arriba ("¿existe AL MENOS UNA
/// regla?"), aquí un nivel más profundo ("¿la regla que existe está completa?").
/// </summary>
public sealed class AccountingChartBackfillServicePostingRuleTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    // No es readonly: Company.Id lo asigna el propio agregado (BaseEntity, setter protegido) — se
    // captura el Id real generado por Company.CreateManaged en SeedActiveCompanyAsync y se usa
    // consistentemente para el resto del test (cuentas/reglas), en vez de forzar un Id externo.
    private Guid _companyId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private ErpDbContext NewDbContext(string dbName) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                // Ver comentario equivalente en AccountingBootstrapStepTests.NewDbContext —
                // TryCorrectLegacyRetentionsDocumentIssuedRule muta líneas de un PostingRule ya
                // trackeado por una query previa; sin este interceptor (solo registrado vía
                // DependencyInjection.cs en producción), EF clasifica mal la línea nueva.
                .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );

    private AccountingChartBackfillService NewService(ErpDbContext db) =>
        new(
            db,
            new FakeHostEnvironment(isProduction: false),
            new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance),
            NullLogger<AccountingChartBackfillService>.Instance
        );

    private async Task SeedActiveCompanyAsync(ErpDbContext db)
    {
        var company = Company.CreateManaged(
            _tenantId,
            "1790012345001",
            "RETQA Backfill Empresa",
            createdBy: _actorId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        // Company.Id lo asigna el propio agregado al construirse — se captura aquí y se usa para
        // el resto del test (cuentas/reglas/ICurrentCompany de los contextos siguientes).
        _companyId = company.Id;
    }

    /// <summary>
    /// Escenario 5(a): company activa con la regla vieja de 2 líneas (01H) — EnsureAsync debe
    /// detectarla como pendiente de backfill (antes de 02C, el chequeo por clave la daba por
    /// completa) y corregirla a 3 líneas al aplicar el backfill.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_detecta_y_corrige_company_con_regla_legacy_de_2_lineas()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
            // Bootstrap completo primero (deja la forma vigente de 3 líneas)...
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            // ...luego se reemplaza por la forma vieja EXACTA de 01H, simulando una company que
            // nunca recibió 02C.
            var rule = await db
                .PostingRules.Include(r => r.Lines)
                .SingleAsync(r =>
                    r.CompanyId == _companyId
                    && r.SourceModule == "Retentions"
                    && r.FactType == "DocumentIssued"
                );
            db.PostingRules.Remove(rule);
            await db.SaveChangesAsync();

            var payablesAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "2.1.01.001")
            ).Id;
            var vatAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "2.1.02.002")
            ).Id;

            var legacyRule = PostingRule.Create(
                _tenantId, _companyId, "Retentions", "DocumentIssued", null, null, null, _actorId
            );
            legacyRule.AddLine(payablesAccountId, AccountNature.Debit, PostingAmountKind.Retention);
            legacyRule.AddLine(vatAccountId, AccountNature.Credit, PostingAmountKind.Retention);
            db.PostingRules.Add(legacyRule);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            await service.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var corrected = await verifyDb
            .PostingRules.Include(r => r.Lines)
            .SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued"
            );
        corrected.Lines.Should().HaveCount(3);
        corrected.Lines.Select(l => l.AmountKind)
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    PostingAmountKind.Retention,
                    PostingAmountKind.RetentionVat,
                    PostingAmountKind.RetentionIncome,
                }
            );
    }

    /// <summary>
    /// Escenario 5(b): company activa completamente sin ninguna PostingRule sembrada recibe el
    /// seed completo (todas las reglas de <c>MinimalPostingRules</c>, incluida la de Retentions ya
    /// con sus 3 líneas) vía backfill — comportamiento ya existente, confirmado tal cual sigue
    /// funcionando con la forma nueva de 3 líneas.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_siembra_regla_completa_de_3_lineas_para_company_sin_ninguna_regla()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            await service.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var rule = await verifyDb
            .PostingRules.Include(r => r.Lines)
            .SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued"
            );
        rule.Lines.Should().HaveCount(3);
    }

    /// <summary>
    /// Escenario 5(c): company activa que ya tiene la regla completa de 3 líneas NO califica para
    /// backfill (EnsureAsync no la toca) — evita correr el step de bootstrap innecesariamente sobre
    /// companies ya al día.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_no_modifica_company_que_ya_tiene_la_regla_completa()
    {
        var dbName = Guid.NewGuid().ToString();
        List<Guid> originalLineIds;

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var rule = await db
                .PostingRules.Include(r => r.Lines)
                .SingleAsync(r =>
                    r.CompanyId == _companyId
                    && r.SourceModule == "Retentions"
                    && r.FactType == "DocumentIssued"
                );
            originalLineIds = rule.Lines.Select(l => l.Id).OrderBy(id => id).ToList();
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            await service.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb
            .PostingRules.Include(r => r.Lines)
            .Where(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued"
            )
            .ToListAsync();
        rules.Should().ContainSingle();
        rules[0]
            .Lines.Select(l => l.Id)
            .OrderBy(id => id)
            .Should()
            .BeEquivalentTo(originalLineIds, because: "ya está completa, EnsureAsync no debe tocarla");
    }

    private sealed class FakeHostEnvironment(bool isProduction) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction ? "Production" : "Development";
        public string ApplicationName { get; set; } = "ERP.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
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
