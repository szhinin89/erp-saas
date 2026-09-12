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
    /// PURCHASE-CREDIT-NOTE-DISCOUNT-POSTING-ACCOUNT-01 — a diferencia del escenario 5(a)
    /// (Retentions, 2 → 3 líneas, detectable por conteo), esta corrección cambia la CUENTA de 2
    /// líneas existentes sin cambiar el conteo (sigue en 4) — EnsureAsync solo la detecta gracias a
    /// <see cref="AccountingBootstrapStep.MatchesLegacyPurchaseCreditNoteAuthorizedForm"/>, no al
    /// chequeo de conteo. Simula una company ya seedeada con la forma vieja (Subtotal/TaxIce
    /// acreditando "1.1.04.001 Inventario mercaderias" en vez de "4.2.01.002 Descuentos obtenidos
    /// en compras") y confirma que el backfill la detecta y corrige.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_detecta_y_corrige_company_con_regla_legacy_de_NC_descuento_acreditando_inventario()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            // Reemplaza la regla vigente (4 líneas, Subtotal/TaxIce → 4.2.01.002) por la forma
            // vieja EXACTA (Subtotal/TaxIce → 1.1.04.001), simulando una company sembrada antes de
            // esta fase.
            var rule = await db
                .PostingRules.Include(r => r.Lines)
                .SingleAsync(r =>
                    r.CompanyId == _companyId
                    && r.SourceModule == "Purchases"
                    && r.FactType == "PurchaseCreditNoteAuthorized"
                );
            db.PostingRules.Remove(rule);
            await db.SaveChangesAsync();

            var payableAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "2.1.01.001")
            ).Id;
            var inventoryAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "1.1.04.001")
            ).Id;
            var vatAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "1.1.05.001")
            ).Id;

            var legacyRule = PostingRule.Create(
                _tenantId, _companyId, "Purchases", "PurchaseCreditNoteAuthorized", null, null, null, _actorId
            );
            legacyRule.AddLine(payableAccountId, AccountNature.Debit, PostingAmountKind.AppliedToPayable);
            legacyRule.AddLine(inventoryAccountId, AccountNature.Credit, PostingAmountKind.Subtotal);
            legacyRule.AddLine(inventoryAccountId, AccountNature.Credit, PostingAmountKind.TaxIce);
            legacyRule.AddLine(vatAccountId, AccountNature.Credit, PostingAmountKind.TaxVat);
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
                r.CompanyId == _companyId
                && r.SourceModule == "Purchases"
                && r.FactType == "PurchaseCreditNoteAuthorized"
            );
        corrected.Lines.Should().HaveCount(4);
        var discountAccountId = (
            await verifyDb.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "4.2.01.002")
        ).Id;
        var inventoryAccountIdAfter = (
            await verifyDb.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "1.1.04.001")
        ).Id;
        corrected.Lines.Where(l => l.AmountKind is PostingAmountKind.Subtotal or PostingAmountKind.TaxIce)
            .Should()
            .OnlyContain(l => l.AccountId == discountAccountId);
        corrected.Lines.Should().NotContain(l => l.AccountId == inventoryAccountIdAfter);
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

    /// <summary>
    /// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01 — escenario real de la fase: una company
    /// activa que ya pasó por el bootstrap ANTES de que "Expenses"/"DocumentConfirmed" existiera en
    /// MinimalPostingRules (simulado quitando esa única regla luego del seed completo) vuelve a
    /// calificar para backfill vía <see cref="AccountingBootstrapStep.RequiredPostingRuleKeys"/> —
    /// mismo mecanismo que ya cerraba este tipo de gap para Retentions en 01H — y recibe solo la
    /// regla faltante, sin duplicar ni tocar las demás.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_siembra_solo_expenses_documentconfirmed_para_company_a_la_que_solo_le_falta_esa()
    {
        var dbName = Guid.NewGuid().ToString();
        List<Guid> otherRuleIds;

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var expensesRule = await db.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Expenses" && r.FactType == "DocumentConfirmed"
            );
            db.PostingRules.Remove(expensesRule);
            await db.SaveChangesAsync();

            otherRuleIds = await db.PostingRules
                .Where(r => r.CompanyId == _companyId)
                .Select(r => r.Id)
                .OrderBy(id => id)
                .ToListAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            await service.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb.PostingRules.Where(r => r.CompanyId == _companyId).ToListAsync();
        rules.Should().Contain(r => r.SourceModule == "Expenses" && r.FactType == "DocumentConfirmed");

        var untouchedIds = rules
            .Where(r => !(r.SourceModule == "Expenses" && r.FactType == "DocumentConfirmed"))
            .Select(r => r.Id)
            .OrderBy(id => id)
            .ToList();
        untouchedIds.Should().BeEquivalentTo(otherRuleIds, because: "el backfill no debe tocar las reglas que ya estaban completas");
    }

    /// <summary>
    /// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01 — una company con una PostingRule
    /// "Expenses"/"DocumentConfirmed" personalizada por un admin (cuentas distintas a las del
    /// blueprint retail) NO se toca: la clave ya existe, así que
    /// <see cref="AccountingBootstrapStep.RequiredPostingRuleKeys"/> la da por completa (mismo
    /// criterio que "no reglas ya editadas por el admin" del resto del seed) — a diferencia de
    /// Retentions/DocumentIssued, esta regla no tiene una forma legacy conocida a corregir.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_no_toca_una_regla_personalizada_de_expenses_documentconfirmed()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid customLineId;

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var expensesRule = await db
                .PostingRules.Include(r => r.Lines)
                .SingleAsync(r =>
                    r.CompanyId == _companyId && r.SourceModule == "Expenses" && r.FactType == "DocumentConfirmed"
                );
            // Admin agrega una línea propia (p. ej. una cuenta puente adicional) — forma distinta
            // de la que el seed produciría, nunca debe revertirse ni completarse silenciosamente.
            var customAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "6.4.01.001")
            ).Id;
            expensesRule.AddLine(customAccountId, AccountNature.Debit, PostingAmountKind.Discount);
            await db.SaveChangesAsync();
            customLineId = expensesRule.Lines.Single(l => l.AmountKind == PostingAmountKind.Discount).Id;
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
                r.CompanyId == _companyId && r.SourceModule == "Expenses" && r.FactType == "DocumentConfirmed"
            );
        rule.Lines.Should().HaveCount(3, because: "la línea personalizada se mantiene, el backfill no la quita ni la altera");
        rule.Lines.Should().Contain(l => l.Id == customLineId);
    }

    /// <summary>
    /// PURCHASE-SUPPLIER-CREDIT-APPLIED-POSTING-RULE-01 — mismo escenario que
    /// "Expenses"/"DocumentConfirmed" (ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01): una company
    /// activa que ya pasó por el bootstrap ANTES de que "Purchases"/"SupplierCreditApplied"
    /// existiera en MinimalPostingRules (simulado quitando esa única regla luego del seed completo)
    /// vuelve a calificar para backfill vía <see cref="AccountingBootstrapStep.RequiredPostingRuleKeys"/>
    /// y recibe solo la regla faltante, sin duplicar ni tocar las demás.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_siembra_solo_purchases_suppliercreditapplied_para_company_a_la_que_solo_le_falta_esa()
    {
        var dbName = Guid.NewGuid().ToString();
        List<Guid> otherRuleIds;

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var supplierCreditRule = await db.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Purchases" && r.FactType == "SupplierCreditApplied"
            );
            db.PostingRules.Remove(supplierCreditRule);
            await db.SaveChangesAsync();

            otherRuleIds = await db.PostingRules
                .Where(r => r.CompanyId == _companyId)
                .Select(r => r.Id)
                .OrderBy(id => id)
                .ToListAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            await service.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb.PostingRules.Where(r => r.CompanyId == _companyId).ToListAsync();
        rules.Should().Contain(r => r.SourceModule == "Purchases" && r.FactType == "SupplierCreditApplied");

        var untouchedIds = rules
            .Where(r => !(r.SourceModule == "Purchases" && r.FactType == "SupplierCreditApplied"))
            .Select(r => r.Id)
            .OrderBy(id => id)
            .ToList();
        untouchedIds.Should().BeEquivalentTo(otherRuleIds, because: "el backfill no debe tocar las reglas que ya estaban completas");
    }

    /// <summary>
    /// PURCHASE-SUPPLIER-CREDIT-APPLICATION-REVERSED-POSTING-RULE-01 — mismo escenario que
    /// "Purchases"/"SupplierCreditApplied": una company activa que ya pasó por el bootstrap ANTES de
    /// que "Purchases"/"SupplierCreditApplicationReversed" existiera en MinimalPostingRules
    /// (simulado quitando esa única regla luego del seed completo) vuelve a calificar para backfill
    /// vía <see cref="AccountingBootstrapStep.RequiredPostingRuleKeys"/> y recibe solo la regla
    /// faltante, sin duplicar ni tocar las demás.
    /// </summary>
    [Fact]
    public async Task EnsureAsync_siembra_solo_purchases_suppliercreditapplicationreversed_para_company_a_la_que_solo_le_falta_esa()
    {
        var dbName = Guid.NewGuid().ToString();
        List<Guid> otherRuleIds;

        await using (var db = NewDbContext(dbName))
        {
            await SeedActiveCompanyAsync(db);
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var reversedRule = await db.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Purchases" && r.FactType == "SupplierCreditApplicationReversed"
            );
            db.PostingRules.Remove(reversedRule);
            await db.SaveChangesAsync();

            otherRuleIds = await db.PostingRules
                .Where(r => r.CompanyId == _companyId)
                .Select(r => r.Id)
                .OrderBy(id => id)
                .ToListAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var service = NewService(db);
            await service.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb.PostingRules.Where(r => r.CompanyId == _companyId).ToListAsync();
        rules.Should().Contain(r => r.SourceModule == "Purchases" && r.FactType == "SupplierCreditApplicationReversed");

        var untouchedReversedIds = rules
            .Where(r => !(r.SourceModule == "Purchases" && r.FactType == "SupplierCreditApplicationReversed"))
            .Select(r => r.Id)
            .OrderBy(id => id)
            .ToList();
        untouchedReversedIds.Should().BeEquivalentTo(otherRuleIds, because: "el backfill no debe tocar las reglas que ya estaban completas");
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
