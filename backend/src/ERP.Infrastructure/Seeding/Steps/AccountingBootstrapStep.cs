using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// ACCOUNTING-INITIAL-CHART-SEED-11: sin este step, una Company nueva nunca tenía Plan de
/// Cuentas ni un AccountingPeriod abierto — el diagnóstico previo (ACCOUNTING-DATA-SEED-AND-
/// SMOKE-10G) confirmó que esto deja al Posting Engine sin ningún <c>PostingRule</c> posible
/// (no hay cuentas a las que apuntar) y, por lo tanto, sin ningún <c>JournalEntry</c> real, aun
/// con ventas/compras operando con normalidad. Plan de cuentas mínimo (Tipo A — infraestructura
/// de sistema, mismo criterio que Caja Principal/Bodega Principal: genérico, editable después
/// por el admin, nunca datos de negocio inventados) — 13 cuentas hoja, sin jerarquía padre/
/// resumen, cubriendo Activo/Pasivo/Patrimonio/Ingresos/Costos/Gastos con la Nature contable
/// estándar de cada tipo. Un único AccountingPeriod anual (PeriodNumber=1, año calendario
/// actual) — suficiente para que <c>PostingPeriodResolver</c> encuentre período abierto para
/// cualquier fecha del año en curso; no crea 12 períodos mensuales (el ticket pide un rango
/// único 01-01..12-31). Idempotente por Code (cuentas) y por FiscalYear (período) — solo crea lo
/// que falta, nunca duplica ni toca cuentas/períodos ya existentes.
///
/// ACCOUNTING-POSTING-RULES-SEED-11B: además del plan de cuentas, siembra las
/// <see cref="PostingRule"/>/<see cref="PostingRuleLine"/> mínimas para los 7 hechos contables que
/// hoy tienen traductor real (ver <c>MinimalPostingRules</c>) — sin esto, el Posting Engine
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

    private sealed record MinimalAccount(
        string Code,
        string Name,
        AccountType Type,
        AccountNature Nature
    );

    private static readonly IReadOnlyList<MinimalAccount> MinimalChart =
    [
        new("1.1.01.001", "Caja General", AccountType.Asset, AccountNature.Debit),
        new("1.1.02.001", "Bancos", AccountType.Asset, AccountNature.Debit),
        new("1.1.03.001", "Cuentas por cobrar clientes", AccountType.Asset, AccountNature.Debit),
        new("1.1.04.001", "Inventario mercaderías", AccountType.Asset, AccountNature.Debit),
        new("1.1.05.001", "IVA crédito tributario", AccountType.Asset, AccountNature.Debit),
        new("2.1.01.001", "Cuentas por pagar proveedores", AccountType.Liability, AccountNature.Credit),
        new("2.1.02.001", "IVA por pagar", AccountType.Liability, AccountNature.Credit),
        new("2.1.03.001", "ICE por pagar", AccountType.Liability, AccountNature.Credit),
        new("3.1.01.001", "Capital", AccountType.Equity, AccountNature.Credit),
        new("3.1.02.001", "Resultados acumulados", AccountType.Equity, AccountNature.Credit),
        new("4.1.01.001", "Ventas", AccountType.Income, AccountNature.Credit),
        new("5.1.01.001", "Costo de ventas", AccountType.Cost, AccountNature.Debit),
        new("6.1.01.001", "Gastos administrativos", AccountType.Expense, AccountNature.Debit),
    ];

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

    // ACCOUNTING-POSTING-RULES-SEED-11B: un (SourceModule, FactType) por cada traductor real
    // confirmado leyendo el código fuente de cada uno (ver doc comment de la clase) —
    // Purchases/PurchaseCreditNoteCancelled queda deliberadamente fuera (reversa el asiento
    // original, no resuelve regla nueva). Mapeos de cuentas genéricos usando exclusivamente las
    // 13 cuentas de MinimalChart — ninguna cuenta ni empresa hardcodeada. Líneas cuyo
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
        new(
            "Finance",
            "SupplierPaymentApplied",
            [
                new("2.1.01.001", AccountNature.Debit, PostingAmountKind.GrandTotal),
                new("1.1.02.001", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
    ];

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;

        var existingCodes = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => a.Code.Value)
            .ToListAsync(cancellationToken);
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.Ordinal);

        var missing = MinimalChart.Where(m => !existingCodeSet.Contains(m.Code)).ToList();
        if (missing.Count > 0)
        {
            foreach (var m in missing)
            {
                var account = Account.Create(
                    tenantId,
                    companyId,
                    AccountCode.Create(m.Code),
                    m.Name,
                    parentAccountId: null,
                    accountType: m.Type,
                    nature: m.Nature,
                    allowsPosting: true,
                    createdBy: actorId
                );
                _db.Accounts.Add(account);
            }
            await _db.SaveChangesAsync(cancellationToken);
            LogAccountsSeeded(missing.Count, companyId);
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

        var accountIdByCode = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new { a.Id, Code = a.Code.Value })
            .ToDictionaryAsync(a => a.Code, a => a.Id, StringComparer.Ordinal, cancellationToken);

        foreach (var m in missingRules)
        {
            var missingAccountCode = m.Lines.Select(l => l.AccountCode)
                .FirstOrDefault(code => !accountIdByCode.ContainsKey(code));
            if (missingAccountCode is not null)
            {
                LogPostingRuleSkippedMissingAccount(m.SourceModule, m.FactType, missingAccountCode, companyId);
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
                rule.AddLine(accountIdByCode[line.AccountCode], line.Nature, line.AmountKind);

            _db.PostingRules.Add(rule);
        }

        await _db.SaveChangesAsync(cancellationToken);
        LogPostingRulesSeeded(missingRules.Count, companyId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded {Count} minimal chart-of-accounts entries for company {CompanyId}."
    )]
    private partial void LogAccountsSeeded(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Chart of accounts already complete for company {CompanyId}. Skipping."
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
        Message = "Seeded {Count} minimal posting rules for company {CompanyId}."
    )]
    private partial void LogPostingRulesSeeded(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Minimal posting rules already complete for company {CompanyId}. Skipping."
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
}
