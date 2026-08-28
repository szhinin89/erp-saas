using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// ACCOUNTING-INITIAL-CHART-SEED-11 / ACCOUNTING-BASE-CHART-TEMPLATE-13: sin este step, una
/// Company nueva nunca tenía Plan de Cuentas ni un AccountingPeriod abierto — el diagnóstico
/// previo (ACCOUNTING-DATA-SEED-AND-SMOKE-10G) confirmó que esto deja al Posting Engine sin
/// ningún <c>PostingRule</c> posible (no hay cuentas a las que apuntar) y, por lo tanto, sin
/// ningún <c>JournalEntry</c> real, aun con ventas/compras operando con normalidad.
///
/// ACCOUNTING-BASE-CHART-TEMPLATE-13 amplía el seed inicial de 13 cuentas hoja a una plantilla
/// retail Ecuador jerárquica: cuentas padre/resumen con <c>AllowsPosting=false</c> y cuentas
/// operativas finales con <c>AllowsPosting=true</c>. La plantilla usa como blueprint el plan real
/// de Sumak, pero no lo copia literalmente: evita cajas/locales/bancos/personas/proveedores
/// específicos y conserva los códigos operativos del seed mínimo previo para que las
/// <c>PostingRule</c> existentes y empresas ya configuradas no se rompan. Idempotente por Code
/// (cuentas) y por FiscalYear (período): solo crea lo faltante, nunca duplica ni toca cuentas/
/// períodos ya existentes.
///
/// ACCOUNTING-POSTING-RULES-SEED-11B: además del plan de cuentas, siembra las
/// <see cref="PostingRule"/>/<see cref="PostingRuleLine"/> mínimas para los hechos contables que
/// hoy tienen traductor real (ver <c>MinimalPostingRules</c>; incluye
/// SUPPLIER-PAYMENTS-POSTING-15D: <c>Payables/SupplierPaymentConfirmed</c>) — sin esto, el Posting Engine
/// resolvía "RULE_NOT_FOUND" (fail-closed, <c>PostingRuleResolver</c>) para toda venta/compra/
/// cobro/pago real, aun con Plan de Cuentas ya sembrado (diagnóstico ACCOUNTING-DATA-SEED-AND-
/// SMOKE-10G). <c>Purchases/PurchaseCreditNoteCancelled</c> NO tiene regla — ese hecho reversa el
/// <c>JournalEntry</c> original vía <c>ReverseJournalEntryCommand</c>
/// (<c>PurchaseCreditNoteCancelledPostingTranslator</c>), nunca resuelve una <c>PostingRule</c>
/// nueva. Idempotente por (SourceModule, FactType): solo crea las reglas que faltan, nunca
/// duplica ni toca reglas ya editadas por el admin (mismo criterio que el plan de cuentas). No
/// genera ningún asiento retroactivo — configuración únicamente.
/// </summary>
public sealed partial class AccountingBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Accounting;

    private readonly ErpDbContext _db;
    private readonly ILogger<AccountingBootstrapStep> _logger;

    public AccountingBootstrapStep(ErpDbContext db, ILogger<AccountingBootstrapStep> logger)
    {
        _db = db;
        _logger = logger;
    }

    private sealed record RetailAccount(
        string Code,
        string Name,
        string? ParentCode,
        AccountType Type,
        AccountNature Nature,
        bool AllowsPosting
    );

    public const int RetailChartAccountCount = 90;

    private static readonly IReadOnlyList<RetailAccount> RetailChart =
    [
        new("1", "Activo", null, AccountType.Asset, AccountNature.Debit, false),
        new("1.1", "Activo corriente", "1", AccountType.Asset, AccountNature.Debit, false),
        new("1.1.01", "Efectivo y equivalentes", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new("1.1.01.001", "Caja general", "1.1.01", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.01.002", "Caja chica", "1.1.01", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.01.003", "Fondos por depositar", "1.1.01", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.02", "Bancos", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new("1.1.02.001", "Bancos cuenta corriente", "1.1.02", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.02.002", "Bancos cuenta ahorros", "1.1.02", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.03", "Cuentas y documentos por cobrar", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new("1.1.03.001", "Cuentas por cobrar clientes", "1.1.03", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.03.002", "Cuentas por cobrar tarjetas credito/debito", "1.1.03", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.03.003", "Otras cuentas por cobrar", "1.1.03", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.03.004", "Anticipos a proveedores", "1.1.03", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.04", "Inventarios", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new("1.1.04.001", "Inventario mercaderias", "1.1.04", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.04.002", "Inventario en transito", "1.1.04", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.04.003", "Inventario por ajustes", "1.1.04", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.05", "Impuestos a favor", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new("1.1.05.001", "IVA credito tributario", "1.1.05", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.05.002", "IVA retenido por clientes", "1.1.05", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.05.003", "Retenciones renta a favor", "1.1.05", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.06", "Pagos anticipados", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new("1.1.06.001", "Seguros pagados por anticipado", "1.1.06", AccountType.Asset, AccountNature.Debit, true),
        new("1.2", "Activo no corriente", "1", AccountType.Asset, AccountNature.Debit, false),
        new("1.2.01", "Propiedad, planta y equipo", "1.2", AccountType.Asset, AccountNature.Debit, false),
        new("1.2.01.001", "Equipos de computacion", "1.2.01", AccountType.Asset, AccountNature.Debit, true),
        new("1.2.01.002", "Muebles y enseres", "1.2.01", AccountType.Asset, AccountNature.Debit, true),
        new("2", "Pasivo", null, AccountType.Liability, AccountNature.Credit, false),
        new("2.1", "Pasivo corriente", "2", AccountType.Liability, AccountNature.Credit, false),
        new("2.1.01", "Cuentas y documentos por pagar", "2.1", AccountType.Liability, AccountNature.Credit, false),
        new("2.1.01.001", "Cuentas por pagar proveedores", "2.1.01", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.01.002", "Anticipos de clientes", "2.1.01", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.02", "IVA por pagar", "2.1", AccountType.Liability, AccountNature.Credit, false),
        new("2.1.02.001", "IVA cobrado en ventas", "2.1.02", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.02.002", "Retenciones IVA por pagar", "2.1.02", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.02.003", "Retenciones renta por pagar", "2.1.02", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.03", "Impuestos especiales por pagar", "2.1", AccountType.Liability, AccountNature.Credit, false),
        new("2.1.03.001", "ICE por pagar", "2.1.03", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.03.002", "IRBP por pagar", "2.1.03", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.03.003", "Impuesto a la renta por pagar", "2.1.03", AccountType.Liability, AccountNature.Credit, true),
        new("2.1.04", "Nomina y beneficios por pagar", "2.1", AccountType.Liability, AccountNature.Credit, false),
        new("2.1.04.001", "Sueldos por pagar", "2.1.04", AccountType.Liability, AccountNature.Credit, true),
        new("3", "Patrimonio", null, AccountType.Equity, AccountNature.Credit, false),
        new("3.1", "Capital y resultados", "3", AccountType.Equity, AccountNature.Credit, false),
        new("3.1.01.001", "Capital", "3.1", AccountType.Equity, AccountNature.Credit, true),
        new("3.1.02.001", "Resultados acumulados", "3.1", AccountType.Equity, AccountNature.Credit, true),
        new("3.1.03.001", "Resultado del ejercicio", "3.1", AccountType.Equity, AccountNature.Credit, true),
        new("4", "Ingresos", null, AccountType.Income, AccountNature.Credit, false),
        new("4.1", "Ingresos operacionales", "4", AccountType.Income, AccountNature.Credit, false),
        new("4.1.01", "Ventas de mercaderia", "4.1", AccountType.Income, AccountNature.Credit, false),
        new("4.1.01.001", "Ventas tarifa general", "4.1.01", AccountType.Income, AccountNature.Credit, true),
        new("4.1.01.002", "Ventas tarifa 0%", "4.1.01", AccountType.Income, AccountNature.Credit, true),
        new("4.1.01.003", "Ventas exentas de IVA", "4.1.01", AccountType.Income, AccountNature.Credit, true),
        new("4.1.01.004", "Ventas no objeto de IVA", "4.1.01", AccountType.Income, AccountNature.Credit, true),
        new("4.1.01.005", "Ventas de servicios retail", "4.1.01", AccountType.Income, AccountNature.Credit, true),
        new("4.2", "Otros ingresos", "4", AccountType.Income, AccountNature.Credit, false),
        new("4.2.01.001", "Ingresos por ajustes positivos de inventario", "4.2", AccountType.Income, AccountNature.Credit, true),
        new("4.2.01.003", "Diferencias positivas de caja", "4.2", AccountType.Income, AccountNature.Credit, true),
        new("5", "Costos", null, AccountType.Cost, AccountNature.Debit, false),
        new("5.1", "Costo de ventas", "5", AccountType.Cost, AccountNature.Debit, false),
        new("5.1.01.001", "Costo de ventas mercaderia", "5.1", AccountType.Cost, AccountNature.Debit, true),
        new("5.1.01.002", "Costo de servicios retail", "5.1", AccountType.Cost, AccountNature.Debit, true),
        new("5.1.01.003", "Costo ICE/IRBP no recuperable", "5.1", AccountType.Cost, AccountNature.Debit, true),
        new("5.1.02", "Ajustes de inventario", "5", AccountType.Cost, AccountNature.Debit, false),
        new("5.1.02.001", "Mermas y faltantes de inventario", "5.1.02", AccountType.Cost, AccountNature.Debit, true),
        new("5.1.02.002", "Descuadres negativos de inventario", "5.1.02", AccountType.Cost, AccountNature.Debit, true),
        new("6", "Gastos", null, AccountType.Expense, AccountNature.Debit, false),
        new("6.1", "Gastos administrativos", "6", AccountType.Expense, AccountNature.Debit, false),
        new("6.1.01.001", "Gastos administrativos generales", "6.1", AccountType.Expense, AccountNature.Debit, true),
        new("6.1.01.002", "Suministros de oficina", "6.1", AccountType.Expense, AccountNature.Debit, true),
        new("6.1.01.003", "Servicios basicos", "6.1", AccountType.Expense, AccountNature.Debit, true),
        new("6.1.01.004", "Arriendos", "6.1", AccountType.Expense, AccountNature.Debit, true),
        new("6.1.01.005", "Honorarios profesionales", "6.1", AccountType.Expense, AccountNature.Debit, true),
        new("6.1.01.006", "Mantenimiento y reparaciones", "6.1", AccountType.Expense, AccountNature.Debit, true),
        new("6.2", "Gastos de venta", "6", AccountType.Expense, AccountNature.Debit, false),
        new("6.2.01.001", "Publicidad y marketing", "6.2", AccountType.Expense, AccountNature.Debit, true),
        new("6.2.01.002", "Comisiones de venta", "6.2", AccountType.Expense, AccountNature.Debit, true),
        new("6.2.01.003", "Empaques, fundas y suministros de venta", "6.2", AccountType.Expense, AccountNature.Debit, true),
        new("6.2.01.004", "Transporte y entregas a clientes", "6.2", AccountType.Expense, AccountNature.Debit, true),
        new("6.3", "Gastos financieros", "6", AccountType.Expense, AccountNature.Debit, false),
        new("6.3.01.001", "Comisiones bancarias", "6.3", AccountType.Expense, AccountNature.Debit, true),
        new("6.3.01.002", "Comisiones tarjetas credito/debito", "6.3", AccountType.Expense, AccountNature.Debit, true),
        new("6.3.01.003", "Intereses financieros", "6.3", AccountType.Expense, AccountNature.Debit, true),
        new("6.4", "Impuestos y no deducibles", "6", AccountType.Expense, AccountNature.Debit, false),
        new("6.4.01.001", "Impuestos no recuperables", "6.4", AccountType.Expense, AccountNature.Debit, true),
        new("6.4.01.002", "Multas y gastos no deducibles", "6.4", AccountType.Expense, AccountNature.Debit, true),
        new("6.5", "Descuadres y perdidas operativas", "6", AccountType.Expense, AccountNature.Debit, false),
        new("6.5.01.001", "Descuadres de caja", "6.5", AccountType.Expense, AccountNature.Debit, true),
        new("6.5.01.002", "Mermas retail", "6.5", AccountType.Expense, AccountNature.Debit, true),
    ];

    internal static readonly IReadOnlyCollection<string> RequiredRetailAccountCodes =
        RetailChart.Select(a => a.Code).ToArray();

    static AccountingBootstrapStep()
    {
        if (RetailChart.Count != RetailChartAccountCount)
            throw new InvalidOperationException(
                $"Retail chart template count mismatch. Expected {RetailChartAccountCount}, got {RetailChart.Count}."
            );
    }

    private sealed record MinimalPostingRuleLine(
        string AccountCode,
        AccountNature Nature,
        PostingAmountKind AmountKind
    );

    private sealed record MinimalPostingRule(
        string SourceModule,
        string FactType,
        IReadOnlyList<MinimalPostingRuleLine> Lines
    );

    private sealed record AccountSeedLookup(Guid Id, bool IsActive, bool AllowsPosting);

    // ACCOUNTING-POSTING-RULES-SEED-11B: un (SourceModule, FactType) por cada traductor real
    // confirmado leyendo el código fuente de cada uno (ver doc comment de la clase) —
    // Purchases/PurchaseCreditNoteCancelled queda deliberadamente fuera (reversa el asiento
    // original, no resuelve regla nueva). Mapeos de cuentas genéricos usando exclusivamente las
    // cuentas de RetailChart — ninguna cuenta ni empresa hardcodeada. Líneas cuyo
    // PostingAmountKind resuelve en 0 (p. ej. ICE/IRBPNR ausentes) se omiten automáticamente en
    // JournalFactory, así que la misma regla sirve con y sin esos componentes.
    private static readonly IReadOnlyList<MinimalPostingRule> MinimalPostingRules =
    [
        new(
            "Sales",
            "InvoiceIssued",
            [
                new("1.1.03.001", AccountNature.Debit, PostingAmountKind.GrandTotal),
                new("4.1.01.001", AccountNature.Credit, PostingAmountKind.Subtotal),
                new("2.1.02.001", AccountNature.Credit, PostingAmountKind.TaxVat),
                new("2.1.03.001", AccountNature.Credit, PostingAmountKind.TaxIce),
            ]
        ),
        new(
            "Sales",
            "CostOfGoodsSold",
            [
                new("5.1.01.001", AccountNature.Debit, PostingAmountKind.HistoricalCost),
                new("1.1.04.001", AccountNature.Credit, PostingAmountKind.HistoricalCost),
            ]
        ),
        new(
            "Sales",
            "CostOfGoodsSoldReversed",
            [
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.HistoricalCost),
                new("5.1.01.001", AccountNature.Credit, PostingAmountKind.HistoricalCost),
            ]
        ),
        new(
            "Purchases",
            "InvoiceReceived",
            [
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.Subtotal),
                new("1.1.05.001", AccountNature.Debit, PostingAmountKind.TaxVat),
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.TaxIce),
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.TaxIrbpnr),
                new("2.1.01.001", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        new(
            "Purchases",
            "PurchaseCreditNoteAuthorized",
            [
                new("2.1.01.001", AccountNature.Debit, PostingAmountKind.AppliedToPayable),
                new("1.1.04.001", AccountNature.Credit, PostingAmountKind.Subtotal),
                new("1.1.04.001", AccountNature.Credit, PostingAmountKind.TaxIce),
                new("1.1.05.001", AccountNature.Credit, PostingAmountKind.TaxVat),
            ]
        ),
        new(
            "Finance",
            "CollectionApplied",
            [
                new("1.1.01.001", AccountNature.Debit, PostingAmountKind.GrandTotal),
                new("1.1.03.001", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        // PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — "Finance"/"SupplierPaymentApplied" (pago a
        // proveedor) se eliminó junto con RegisterPaymentCommand/SupplierPaymentAppliedPostingTranslator:
        // sin nada que dispare ese FactType, sembrar la PostingRule por empresa nueva sería
        // configuración muerta desde el día uno.
        //
        // SUPPLIER-PAYMENTS-POSTING-15D — "Payables"/"SupplierPaymentConfirmed" reemplaza esa
        // configuración muerta: única línea fija (Debe CxP por el total del pago); el Haber por
        // cada medio de pago (1..N líneas, caja/banco) es completamente dinámico vía
        // PostingFact.Allocations en SupplierPaymentConfirmedPostingTranslator, no representable
        // como PostingRuleLine fija (cardinalidad variable, mismo criterio que
        // "Expenses"/"DocumentConfirmed").
        new(
            "Payables",
            "SupplierPaymentConfirmed",
            [new("2.1.01.001", AccountNature.Debit, PostingAmountKind.GrandTotal)]
        ),
    ];

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;

        var existingAccounts = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new { a.Id, Code = a.Code.Value })
            .ToListAsync(cancellationToken);
        var accountIdByCode = existingAccounts.ToDictionary(
            a => a.Code,
            a => a.Id,
            StringComparer.Ordinal
        );

        var seededCount = 0;
        foreach (var m in RetailChart)
        {
            if (accountIdByCode.ContainsKey(m.Code))
                continue;

            var parentAccountId = m.ParentCode is null ? (Guid?)null : accountIdByCode[m.ParentCode];
            var account = Account.Create(
                tenantId,
                companyId,
                AccountCode.Create(m.Code),
                m.Name,
                parentAccountId,
                m.Type,
                m.Nature,
                m.AllowsPosting,
                actorId
            );
            _db.Accounts.Add(account);
            accountIdByCode[m.Code] = account.Id;
            seededCount++;
        }

        if (seededCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            LogAccountsSeeded(seededCount, companyId);
        }
        else
        {
            LogAccountsSkipped(companyId);
        }

        var currentYear = DateTime.UtcNow.Year;
        var hasPeriodForYear = await _db
            .AccountingPeriods.IgnoreQueryFilters()
            .AnyAsync(
                p => p.TenantId == tenantId && p.CompanyId == companyId && p.FiscalYear == currentYear,
                cancellationToken
            );

        if (!hasPeriodForYear)
        {
            var period = AccountingPeriod.Create(
                tenantId,
                companyId,
                currentYear,
                periodNumber: 1,
                startDate: new DateOnly(currentYear, 1, 1),
                endDate: new DateOnly(currentYear, 12, 31),
                createdBy: actorId
            );
            _db.AccountingPeriods.Add(period);
            await _db.SaveChangesAsync(cancellationToken);
            LogPeriodSeeded(currentYear, companyId);
        }
        else
        {
            LogPeriodSkipped(currentYear, companyId);
        }

        await SeedPostingRulesAsync(tenantId, companyId, actorId, cancellationToken);
    }

    private async Task SeedPostingRulesAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        var existingRuleKeys = await _db
            .PostingRules.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId)
            .Select(r => new { r.SourceModule, r.FactType })
            .ToListAsync(cancellationToken);
        var existingRuleKeySet = existingRuleKeys
            .Select(r => (r.SourceModule, r.FactType))
            .ToHashSet();

        var missingRules = MinimalPostingRules
            .Where(r => !existingRuleKeySet.Contains((r.SourceModule, r.FactType)))
            .ToList();

        if (missingRules.Count == 0)
        {
            LogPostingRulesSkipped(companyId);
            return;
        }

        var accountByCode = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new
            {
                a.Id,
                Code = a.Code.Value,
                a.IsActive,
                a.AllowsPosting,
            })
            .ToDictionaryAsync(
                a => a.Code,
                a => new AccountSeedLookup(a.Id, a.IsActive, a.AllowsPosting),
                StringComparer.Ordinal,
                cancellationToken
            );

        var seededRulesCount = 0;
        foreach (var m in missingRules)
        {
            var missingAccountCode = m.Lines.Select(l => l.AccountCode)
                .FirstOrDefault(code => !accountByCode.ContainsKey(code));
            if (missingAccountCode is not null)
            {
                LogPostingRuleSkippedMissingAccount(m.SourceModule, m.FactType, missingAccountCode, companyId);
                continue;
            }

            var invalidAccountCode = m.Lines.Select(l => l.AccountCode)
                .FirstOrDefault(code =>
                    !accountByCode[code].IsActive || !accountByCode[code].AllowsPosting
                );
            if (invalidAccountCode is not null)
            {
                LogPostingRuleSkippedInvalidAccount(m.SourceModule, m.FactType, invalidAccountCode, companyId);
                continue;
            }

            var rule = PostingRule.Create(
                tenantId,
                companyId,
                m.SourceModule,
                m.FactType,
                debitAccountId: null,
                creditAccountId: null,
                taxCode: null,
                createdBy: actorId
            );

            foreach (var line in m.Lines)
                rule.AddLine(accountByCode[line.AccountCode].Id, line.Nature, line.AmountKind);

            _db.PostingRules.Add(rule);
            seededRulesCount++;
        }

        if (seededRulesCount == 0)
        {
            LogPostingRulesSkipped(companyId);
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
        LogPostingRulesSeeded(seededRulesCount, companyId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded {Count} retail chart-of-accounts entries for company {CompanyId}."
    )]
    private partial void LogAccountsSeeded(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retail chart of accounts already complete for company {CompanyId}. Skipping."
    )]
    private partial void LogAccountsSkipped(Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded AccountingPeriod {FiscalYear} for company {CompanyId}."
    )]
    private partial void LogPeriodSeeded(int fiscalYear, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "AccountingPeriod {FiscalYear} already exists for company {CompanyId}. Skipping."
    )]
    private partial void LogPeriodSkipped(int fiscalYear, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded {Count} retail posting rules for company {CompanyId}."
    )]
    private partial void LogPostingRulesSeeded(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retail posting rules already complete for company {CompanyId}. Skipping."
    )]
    private partial void LogPostingRulesSkipped(Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped seeding posting rule {SourceModule}/{FactType} for company {CompanyId} — "
            + "referenced account code {AccountCode} not found in the chart of accounts."
    )]
    private partial void LogPostingRuleSkippedMissingAccount(
        string sourceModule,
        string factType,
        string accountCode,
        Guid companyId
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped seeding posting rule {SourceModule}/{FactType} for company {CompanyId} — "
            + "referenced account code {AccountCode} is inactive or does not allow posting."
    )]
    private partial void LogPostingRuleSkippedInvalidAccount(
        string sourceModule,
        string factType,
        string accountCode,
        Guid companyId
    );
}
