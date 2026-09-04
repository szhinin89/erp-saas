using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Services;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// ACCOUNTING-INITIAL-CHART-SEED-11 — cubre lo que el diagnóstico previo (ACCOUNTING-DATA-SEED-
/// AND-SMOKE-10G) encontró vacío: sin este step, ninguna Company tiene Plan de Cuentas ni
/// AccountingPeriod, así que el Posting Engine nunca encuentra una PostingRule (no hay cuentas a
/// las que apuntar) — cero JournalEntry aun con documentos operativos reales. Usa InMemory (no
/// Testcontainers): el step solo hace LINQ/Add/SaveChanges estándar, sin SQL específico de
/// Postgres.
/// </summary>
public sealed class AccountingBootstrapStepTests
{
    /// <summary>
    /// ACCOUNTING-BOOTSTRAP-TESTS-FIX: <c>MinimalPostingRules</c> tiene 8 entradas desde
    /// SUPPLIER-PAYMENTS-POSTING-15D/SUPPLIER-PAYMENTS-REVERSE-16 (agregaron "Payables"/
    /// "SupplierPaymentConfirmed" y "Payables"/"SupplierPaymentReversed"). Antes eran 6 — los
    /// tests quedaron desactualizados y fallaban contra el código real, no al revés.
    ///
    /// RETENTIONS-POSTING-RULE-SEED-01H: pasa de 8 a 9 — agrega "Retentions"/"DocumentIssued".
    ///
    /// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01: pasa de 9 a 10 — agrega
    /// "Expenses"/"DocumentConfirmed", que nunca había sido sembrada.
    /// </summary>
    private const int ExpectedPostingRulesCount = 10;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private ErpDbContext NewDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)
            )
            // RETENTIONS-TAX-COMPONENT-POSTING-02C — TryCorrectLegacyRetentionsDocumentIssuedRule
            // agrega/quita líneas de un PostingRule ya trackeado por una query previa en el mismo
            // DbContext (mismo patrón que otros tests de este repo que mutan hijos de un agregado
            // ya cargado, ver AuthorizePurchaseReturnConcurrencyTests) — sin este interceptor
            // (normalmente registrado solo vía DependencyInjection.cs en producción), EF clasifica
            // mal la línea nueva como Modified en vez de Added.
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    [Fact]
    public async Task Primera_ejecucion_crea_plantilla_retail_y_un_periodo_anual_abierto()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var accounts = await db.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        accounts.Should().HaveCount(AccountingBootstrapStep.RetailChartAccountCount);
        accounts.Should().OnlyContain(a => a.IsActive);
        accounts
            .Where(a => a.ParentAccountId is null)
            .Should()
            .OnlyContain(a => !a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "1.1.01.001" && a.Name == "Caja general");
        accounts.Should().Contain(a => a.Code.Value == "1.1.01.002" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "1.1.03.002" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "4.1.01.002" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "6.5.01.001" && a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "1" && !a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "2" && !a.AllowsPosting);
        accounts.Should().Contain(a => a.Code.Value == "4.1.01" && !a.AllowsPosting);
        var cashParent = accounts.Single(a => a.Code.Value == "1.1.01");
        accounts
            .Single(a => a.Code.Value == "1.1.01.001")
            .ParentAccountId.Should()
            .Be(cashParent.Id);
        accounts
            .Where(a => a.AccountType is AccountType.Asset or AccountType.Cost or AccountType.Expense)
            .Should()
            .OnlyContain(a => a.Nature == AccountNature.Debit);
        accounts
            .Where(a => a.AccountType is AccountType.Liability or AccountType.Equity or AccountType.Income)
            .Should()
            .OnlyContain(a => a.Nature == AccountNature.Credit);

        var periods = await db.AccountingPeriods.Where(p => p.CompanyId == _companyId).ToListAsync();
        periods.Should().ContainSingle();
        periods[0].FiscalYear.Should().Be(DateTime.UtcNow.Year);
        periods[0].StartDate.Should().Be(new DateOnly(DateTime.UtcNow.Year, 1, 1));
        periods[0].EndDate.Should().Be(new DateOnly(DateTime.UtcNow.Year, 12, 31));
        periods[0].Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Ejecutar_dos_veces_no_duplica_cuentas_ni_periodo()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        (await verifyDb.Accounts.CountAsync(a => a.CompanyId == _companyId))
            .Should()
            .Be(AccountingBootstrapStep.RetailChartAccountCount);
        (await verifyDb.AccountingPeriods.CountAsync(p => p.CompanyId == _companyId)).Should().Be(1);
    }

    [Fact]
    public async Task Si_falta_solo_una_cuenta_crea_unicamente_la_faltante()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var toRemove = await db.Accounts.SingleAsync(a =>
                a.CompanyId == _companyId && a.Code.Value == "6.1.01.001"
            );
            db.Accounts.Remove(toRemove);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var accounts = await verifyDb.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        accounts.Should().HaveCount(AccountingBootstrapStep.RetailChartAccountCount);
        accounts.Should()
            .Contain(a => a.Code.Value == "6.1.01.001" && a.Name == "Gastos administrativos generales");
    }

    [Fact]
    public async Task Si_empresa_tenia_seed_minimo_crea_solo_las_cuentas_retail_faltantes()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var legacyAccounts = new[]
            {
                ("1.1.01.001", "Caja General Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.02.001", "Bancos Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.03.001", "Cuentas por cobrar clientes Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.04.001", "Inventario mercaderías Legacy", AccountType.Asset, AccountNature.Debit),
                ("1.1.05.001", "IVA crédito tributario Legacy", AccountType.Asset, AccountNature.Debit),
                ("2.1.01.001", "Cuentas por pagar proveedores Legacy", AccountType.Liability, AccountNature.Credit),
                ("2.1.02.001", "IVA por pagar Legacy", AccountType.Liability, AccountNature.Credit),
                ("2.1.03.001", "ICE por pagar Legacy", AccountType.Liability, AccountNature.Credit),
                ("3.1.01.001", "Capital Legacy", AccountType.Equity, AccountNature.Credit),
                ("3.1.02.001", "Resultados acumulados Legacy", AccountType.Equity, AccountNature.Credit),
                ("4.1.01.001", "Ventas Legacy", AccountType.Income, AccountNature.Credit),
                ("5.1.01.001", "Costo de ventas Legacy", AccountType.Cost, AccountNature.Debit),
                ("6.1.01.001", "Gastos administrativos Legacy", AccountType.Expense, AccountNature.Debit),
            };

            foreach (var (code, name, type, nature) in legacyAccounts)
            {
                db.Accounts.Add(
                    ERP.Domain.Modules.Accounting.Entities.Account.Create(
                        _tenantId,
                        _companyId,
                        ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create(code),
                        name,
                        parentAccountId: null,
                        accountType: type,
                        nature: nature,
                        allowsPosting: true,
                        createdBy: _actorId
                    )
                );
            }
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var accounts = await verifyDb.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        accounts.Should().HaveCount(AccountingBootstrapStep.RetailChartAccountCount);
        accounts.Should().ContainSingle(a => a.Code.Value == "1.1.01.001");
        accounts.Single(a => a.Code.Value == "1.1.01.001").Name.Should().Be("Caja General Legacy");
        accounts.Single(a => a.Code.Value == "1.1.01.001").ParentAccountId.Should().BeNull();
        accounts.Should().Contain(a => a.Code.Value == "1.1.01.002" && a.Name == "Caja chica");
        accounts.Should().Contain(a => a.Code.Value == "4.1.01.002" && a.Name == "Ventas tarifa 0%");
    }

    [Fact]
    public async Task No_sobrescribe_cuenta_existente_editada_por_usuario()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            db.Accounts.Add(
                ERP.Domain.Modules.Accounting.Entities.Account.Create(
                    _tenantId,
                    _companyId,
                    ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.01.001"),
                    "Caja editada por admin",
                    parentAccountId: null,
                    accountType: AccountType.Asset,
                    nature: AccountNature.Debit,
                    allowsPosting: false,
                    createdBy: _actorId
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var edited = await verifyDb.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code.Value == "1.1.01.001"
        );
        edited.Name.Should().Be("Caja editada por admin");
        edited.AllowsPosting.Should().BeFalse();
        edited.ParentAccountId.Should().BeNull();
    }

    [Fact]
    public async Task Primera_ejecucion_crea_8_posting_rules_minimas_una_por_cada_traductor_real()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var rules = await db
            .PostingRules.Include(r => r.Lines)
            .Where(r => r.CompanyId == _companyId)
            .ToListAsync();

        rules.Should().HaveCount(ExpectedPostingRulesCount);
        rules.Should().OnlyContain(r => r.IsActive);
        rules
            .Select(r => (r.SourceModule, r.FactType))
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    ("Sales", "InvoiceIssued"),
                    ("Sales", "CostOfGoodsSold"),
                    ("Sales", "CostOfGoodsSoldReversed"),
                    ("Purchases", "InvoiceReceived"),
                    ("Purchases", "PurchaseCreditNoteAuthorized"),
                    ("Finance", "CollectionApplied"),
                    ("Payables", "SupplierPaymentConfirmed"),
                    ("Payables", "SupplierPaymentReversed"),
                    ("Retentions", "DocumentIssued"),
                    ("Expenses", "DocumentConfirmed"),
                }
            );

        // Las 7 reglas "clásicas" (incluida "Expenses"/"DocumentConfirmed" desde
        // ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01) tienen todas sus líneas fijas (>=2). Las 2
        // reglas de Pagos a Proveedores (SUPPLIER-PAYMENTS-POSTING-15D/SUPPLIER-PAYMENTS-REVERSE-16)
        // solo fijan la línea de CxP — el Haber/Debe por cada medio de pago es dinámico vía
        // PostingFact.Allocations, no representable como PostingRuleLine, así que cada una tiene
        // exactamente 1 línea fija.
        var rulesWithFixedTwoOrMoreLines = rules.Where(r =>
            r.SourceModule != "Payables"
        );
        rulesWithFixedTwoOrMoreLines.Should().OnlyContain(r => r.Lines.Count >= 2);

        var supplierPaymentConfirmed = rules.Single(r =>
            r.SourceModule == "Payables" && r.FactType == "SupplierPaymentConfirmed"
        );
        supplierPaymentConfirmed.Lines.Should().ContainSingle();
        var confirmedLine = supplierPaymentConfirmed.Lines.Single();
        confirmedLine.Nature.Should().Be(AccountNature.Debit);
        confirmedLine.AmountKind.Should().Be(PostingAmountKind.GrandTotal);
        (await db.Accounts.SingleAsync(a => a.Id == confirmedLine.AccountId)).Code.Value
            .Should()
            .Be("2.1.01.001");

        var supplierPaymentReversed = rules.Single(r =>
            r.SourceModule == "Payables" && r.FactType == "SupplierPaymentReversed"
        );
        supplierPaymentReversed.Lines.Should().ContainSingle();
        var reversedLine = supplierPaymentReversed.Lines.Single();
        reversedLine.Nature.Should().Be(AccountNature.Credit);
        reversedLine.AmountKind.Should().Be(PostingAmountKind.GrandTotal);
        (await db.Accounts.SingleAsync(a => a.Id == reversedLine.AccountId)).Code.Value
            .Should()
            .Be("2.1.01.001");

        // PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — "Finance"/"SupplierPaymentApplied" ya no se siembra
        // (sin RegisterPaymentCommand/traductor que lo dispare, sería configuración muerta).
        rules.Should().NotContain(r => r.SourceModule == "Finance" && r.FactType == "SupplierPaymentApplied");
        rules.Should()
            .NotContain(r => r.SourceModule == "Purchases" && r.FactType == "PurchaseCreditNoteCancelled");
    }

    /// <summary>
    /// RETENTIONS-POSTING-RULE-SEED-01H / RETENTIONS-TAX-COMPONENT-POSTING-02C — el seed crea la
    /// PostingRule de Retentions con las 3 líneas exactas del ejemplo conceptual de
    /// docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § "Impacto contable" separado por componente
    /// tributario: Debe CxP proveedor (total, <see cref="PostingAmountKind.Retention"/>), Haber
    /// Retenciones IVA por pagar (<see cref="PostingAmountKind.RetentionVat"/>), Haber Retenciones
    /// Renta por pagar (<see cref="PostingAmountKind.RetentionIncome"/>) — 01H sembraba solo 2
    /// líneas (IVA+Renta combinados bajo <c>Retention</c>); 02C separa el crédito.
    /// </summary>
    [Fact]
    public async Task Seed_crea_postingrule_retentions_documentissued_con_debe_cxp_y_haber_iva_y_renta_separados()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var rule = await db
            .PostingRules.Include(r => r.Lines)
            .SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued"
            );

        rule.IsActive.Should().BeTrue();
        rule.Lines.Should().HaveCount(3);

        var debitLine = rule.Lines.Should().ContainSingle(l => l.Nature == AccountNature.Debit).Which;
        debitLine.AmountKind.Should().Be(PostingAmountKind.Retention);
        (await db.Accounts.SingleAsync(a => a.Id == debitLine.AccountId)).Code.Value
            .Should()
            .Be("2.1.01.001", because: "Debe = CxP proveedor, cuenta genérica ya usada por el resto del ERP");

        var creditLines = rule.Lines.Where(l => l.Nature == AccountNature.Credit).ToList();
        creditLines.Should().HaveCount(2);

        var vatLine = creditLines.Should().ContainSingle(l => l.AmountKind == PostingAmountKind.RetentionVat).Which;
        (await db.Accounts.SingleAsync(a => a.Id == vatLine.AccountId)).Code.Value
            .Should()
            .Be("2.1.02.002", because: "Haber = Retenciones IVA por pagar, cuenta canónica del plan retail");

        var incomeLine = creditLines.Should().ContainSingle(l => l.AmountKind == PostingAmountKind.RetentionIncome).Which;
        (await db.Accounts.SingleAsync(a => a.Id == incomeLine.AccountId)).Code.Value
            .Should()
            .Be("2.1.02.003", because: "Haber = Retenciones Renta por pagar, cuenta canónica del plan retail ya sembrada sin usar");
    }

    /// <summary>
    /// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01 — la PostingRule de "Expenses"/"DocumentConfirmed"
    /// nunca existió en MinimalPostingRules (a diferencia de Retentions/DocumentIssued, que 01H sí
    /// sembraba, aunque incompleta). Confirma las 2 líneas fijas esperadas: Debe IVA crédito
    /// tributario (si el gasto tiene IVA) y Haber CxP proveedores por el total — la línea Debe por
    /// cada categoría/subcategoría de gasto es dinámica vía PostingFact.Allocations en
    /// ExpenseDocumentConfirmedPostingTranslator, no representable como PostingRuleLine fija.
    /// </summary>
    [Fact]
    public async Task Seed_crea_postingrule_expenses_documentconfirmed_con_debe_iva_y_haber_cxp()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var rule = await db
            .PostingRules.Include(r => r.Lines)
            .SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Expenses" && r.FactType == "DocumentConfirmed"
            );

        rule.IsActive.Should().BeTrue();
        rule.Lines.Should().HaveCount(2);

        var debitLine = rule.Lines.Should().ContainSingle(l => l.Nature == AccountNature.Debit).Which;
        debitLine.AmountKind.Should().Be(PostingAmountKind.TaxVat);
        (await db.Accounts.SingleAsync(a => a.Id == debitLine.AccountId)).Code.Value
            .Should()
            .Be("1.1.05.001", because: "Debe = IVA crédito tributario, se omite en cero si el gasto no tiene IVA");

        var creditLine = rule.Lines.Should().ContainSingle(l => l.Nature == AccountNature.Credit).Which;
        creditLine.AmountKind.Should().Be(PostingAmountKind.GrandTotal);
        (await db.Accounts.SingleAsync(a => a.Id == creditLine.AccountId)).Code.Value
            .Should()
            .Be("2.1.01.001", because: "Haber = CxP proveedores por el total del gasto");
    }

    /// <summary>
    /// RETENTIONS-POSTING-RULE-SEED-01H — caso 6 del plan de tests: si a la empresa le falta (o
    /// no permite asiento en) la cuenta canónica de Retenciones IVA por pagar, el seed NO crea la
    /// PostingRule de Retentions — mismo criterio fail-closed ya usado por el resto de
    /// MinimalPostingRules (ver No_crea_posting_rule_si_una_cuenta_requerida_no_permite_asiento) —
    /// nunca crea una regla con una cuenta inválida/silenciosa. Las demás reglas no se ven afectadas.
    /// </summary>
    [Fact]
    public async Task No_crea_posting_rule_de_retentions_si_la_cuenta_de_retencion_iva_no_permite_asiento()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            db.Accounts.Add(
                ERP.Domain.Modules.Accounting.Entities.Account.Create(
                    _tenantId,
                    _companyId,
                    ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("2.1.02.002"),
                    "Retenciones IVA por pagar no postable",
                    parentAccountId: null,
                    accountType: AccountType.Liability,
                    nature: AccountNature.Credit,
                    allowsPosting: false,
                    createdBy: _actorId
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb.PostingRules.Where(r => r.CompanyId == _companyId).ToListAsync();
        rules.Should().NotContain(r => r.SourceModule == "Retentions" && r.FactType == "DocumentIssued");
        rules.Should().Contain(r => r.SourceModule == "Sales" && r.FactType == "InvoiceIssued");
        rules.Should().Contain(r => r.SourceModule == "Payables" && r.FactType == "SupplierPaymentConfirmed");
    }

    /// <summary>
    /// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01 — mismo criterio fail-closed que el resto de
    /// MinimalPostingRules: si la cuenta canónica de IVA crédito tributario no permite asiento, el
    /// seed NO crea la PostingRule de Expenses (nunca con una cuenta inválida/silenciosa). Las
    /// demás reglas no se ven afectadas.
    /// </summary>
    [Fact]
    public async Task No_crea_posting_rule_de_expenses_si_la_cuenta_de_iva_no_permite_asiento()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            db.Accounts.Add(
                ERP.Domain.Modules.Accounting.Entities.Account.Create(
                    _tenantId,
                    _companyId,
                    ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.05.001"),
                    "IVA credito tributario no postable",
                    parentAccountId: null,
                    accountType: AccountType.Asset,
                    nature: AccountNature.Debit,
                    allowsPosting: false,
                    createdBy: _actorId
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb.PostingRules.Where(r => r.CompanyId == _companyId).ToListAsync();
        rules.Should().NotContain(r => r.SourceModule == "Expenses" && r.FactType == "DocumentConfirmed");
        rules.Should().Contain(r => r.SourceModule == "Sales" && r.FactType == "InvoiceIssued");
        rules.Should().Contain(r => r.SourceModule == "Retentions" && r.FactType == "DocumentIssued");
    }

    [Fact]
    public async Task No_crea_posting_rule_si_una_cuenta_requerida_no_permite_asiento()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            db.Accounts.Add(
                ERP.Domain.Modules.Accounting.Entities.Account.Create(
                    _tenantId,
                    _companyId,
                    ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.01.001"),
                    "Caja no postable",
                    parentAccountId: null,
                    accountType: AccountType.Asset,
                    nature: AccountNature.Debit,
                    allowsPosting: false,
                    createdBy: _actorId
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb.PostingRules.Where(r => r.CompanyId == _companyId).ToListAsync();
        rules.Should()
            .NotContain(r => r.SourceModule == "Finance" && r.FactType == "CollectionApplied");
        rules.Should().Contain(r => r.SourceModule == "Sales" && r.FactType == "InvoiceIssued");
    }

    [Fact]
    public async Task Cada_posting_rule_referencia_solo_cuentas_del_plan_sembrado()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var accountIds = (await db.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync())
            .Select(a => a.Id)
            .ToHashSet();
        var rules = await db
            .PostingRules.Include(r => r.Lines)
            .Where(r => r.CompanyId == _companyId)
            .ToListAsync();

        rules.SelectMany(r => r.Lines).Should().OnlyContain(l => accountIds.Contains(l.AccountId));
    }

    [Fact]
    public async Task Ejecutar_dos_veces_no_duplica_posting_rules()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        (await verifyDb.PostingRules.CountAsync(r => r.CompanyId == _companyId))
            .Should()
            .Be(ExpectedPostingRulesCount);
    }

    [Fact]
    public async Task No_toca_una_posting_rule_ya_editada_por_el_admin()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var rule = await db.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Sales" && r.FactType == "InvoiceIssued"
            );
            rule.Disable(_actorId);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb
            .PostingRules.Where(r =>
                r.CompanyId == _companyId && r.SourceModule == "Sales" && r.FactType == "InvoiceIssued"
            )
            .ToListAsync();
        rules.Should()
            .ContainSingle(because: "la regla ya existe (aunque deshabilitada) — el seed nunca re-crea ni reactiva una regla existente")
            .Which.IsActive.Should()
            .BeFalse();
    }

    /// <summary>
    /// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 8/Test de bootstrap: la plantilla RetailChart
    /// sembrada debe pasar el diagnóstico de invariantes sin ningún hallazgo — 0 padres
    /// faltantes/desalineados, 0 diferencias Level vs profundidad de código, 0 ciclos, 0 cuentas
    /// con hijas y AllowsPosting=true.
    /// </summary>
    [Fact]
    public async Task Plantilla_retail_sembrada_no_tiene_inconsistencias_de_jerarquia()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var accounts = await db.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        var postingRules = await db
            .PostingRules.Include(r => r.Lines)
            .Where(r => r.CompanyId == _companyId)
            .ToListAsync();
        var report = AccountHierarchyDiagnostics.Analyze(accounts, postingRules);

        report.Issues.Should().BeEmpty();
    }

    /// <summary>
    /// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 2: las 10 cuentas agrupadoras intermedias
    /// faltantes (código de 4 segmentos parentado directo bajo uno de 2) quedan creadas con el
    /// padre canónico correcto, y el bug de dato preexistente ("5.1.02" bajo "5" en vez de "5.1")
    /// queda corregido.
    /// </summary>
    [Fact]
    public async Task Crea_las_10_cuentas_agrupadoras_intermedias_faltantes_con_padre_canonico()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var accounts = await db.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync();
        var byCode = accounts.ToDictionary(a => a.Code.Value);

        var expectedIntermediates = new[]
        {
            ("3.1.01", "3.1"),
            ("3.1.02", "3.1"),
            ("3.1.03", "3.1"),
            ("4.2.01", "4.2"),
            ("5.1.01", "5.1"),
            ("6.1.01", "6.1"),
            ("6.2.01", "6.2"),
            ("6.3.01", "6.3"),
            ("6.4.01", "6.4"),
            ("6.5.01", "6.5"),
        };

        foreach (var (code, parentCode) in expectedIntermediates)
        {
            byCode.Should().ContainKey(code);
            var account = byCode[code];
            account.AllowsPosting.Should().BeFalse();
            account.IsActive.Should().BeTrue();
            account.ParentAccountId.Should().Be(byCode[parentCode].Id);
        }

        byCode["5.1.02"].ParentAccountId.Should().Be(byCode["5.1"].Id);
    }

    // ══════════════════════════════════════════════════════════════════════
    // RETENTIONS-TAX-COMPONENT-POSTING-02C — corrección de la regla legacy de 2 líneas (01H)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Escenario 5(a) del plan de tests: una company que ya tenía sembrada la forma vieja EXACTA
    /// de 01H (2 líneas, Haber único con <see cref="PostingAmountKind.Retention"/> contra la
    /// cuenta de IVA) recibe la corrección al volver a correr <c>ExecuteAsync</c> — queda con 3
    /// líneas (Debe CxP sin cambios, Haber IVA con <c>RetentionVat</c>, Haber Renta nueva con
    /// <c>RetentionIncome</c>), sin duplicar el Debe.
    /// </summary>
    [Fact]
    public async Task Empresa_con_regla_legacy_de_2_lineas_recibe_correccion_a_3_lineas()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid debitLineId;

        await using (var db = NewDbContext(dbName))
        {
            // Seed inicial (crea el plan de cuentas, incluida 2.1.02.003 sin usar) y luego se
            // reemplazan las líneas de Retentions/DocumentIssued por la forma vieja de 01H —
            // simula una company que pasó por el seed antes de que 02C existiera.
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
            db.PostingRules.Remove(rule);
            await db.SaveChangesAsync();

            var payablesAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "2.1.01.001")
            ).Id;
            var vatAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "2.1.02.002")
            ).Id;

            var legacyRule = ERP.Domain.Modules.Accounting.Entities.PostingRule.Create(
                _tenantId, _companyId, "Retentions", "DocumentIssued", null, null, null, _actorId
            );
            legacyRule.AddLine(payablesAccountId, AccountNature.Debit, PostingAmountKind.Retention);
            legacyRule.AddLine(vatAccountId, AccountNature.Credit, PostingAmountKind.Retention);
            db.PostingRules.Add(legacyRule);
            await db.SaveChangesAsync();
            debitLineId = legacyRule.Lines.Single(l => l.Nature == AccountNature.Debit).Id;
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var corrected = await verifyDb
            .PostingRules.Include(r => r.Lines)
            .SingleAsync(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued"
            );

        corrected.Lines.Should().HaveCount(3);
        corrected
            .Lines.Should()
            .ContainSingle(l => l.Id == debitLineId, because: "el Debe de CxP proveedor no se toca")
            .Which.AmountKind.Should()
            .Be(PostingAmountKind.Retention);
        corrected.Lines.Where(l => l.Nature == AccountNature.Credit).Select(l => l.AmountKind)
            .Should()
            .BeEquivalentTo(new[] { PostingAmountKind.RetentionVat, PostingAmountKind.RetentionIncome });
        var incomeAccountId = corrected
            .Lines.Single(l => l.AmountKind == PostingAmountKind.RetentionIncome)
            .AccountId;
        (await verifyDb.Accounts.SingleAsync(a => a.Id == incomeAccountId)).Code.Value
            .Should()
            .Be("2.1.02.003");
    }

    /// <summary>
    /// Escenario 5(c) del plan de tests: una company que ya tiene la forma vigente (3 líneas) no
    /// se modifica al volver a correr <c>ExecuteAsync</c> — mismos Ids de línea, sin duplicar ni
    /// re-insertar nada (mismo criterio de idempotencia del resto del seed).
    /// </summary>
    [Fact]
    public async Task Empresa_con_regla_ya_completa_de_3_lineas_no_se_modifica()
    {
        var dbName = Guid.NewGuid().ToString();
        List<Guid> originalLineIds;

        await using (var db = NewDbContext(dbName))
        {
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
            var step = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var rules = await verifyDb
            .PostingRules.Include(r => r.Lines)
            .Where(r =>
                r.CompanyId == _companyId && r.SourceModule == "Retentions" && r.FactType == "DocumentIssued"
            )
            .ToListAsync();

        rules.Should().ContainSingle();
        var finalLineIds = rules[0].Lines.Select(l => l.Id).OrderBy(id => id).ToList();
        finalLineIds.Should().BeEquivalentTo(originalLineIds, because: "no debe agregar, quitar ni recrear líneas ya correctas");
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
