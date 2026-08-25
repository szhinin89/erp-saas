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
/// que falta, nunca duplica ni toca cuentas/períodos ya existentes. No crea PostingRules (fuera
/// de alcance de este ticket, ver ACCOUNTING-POSTING-RULES-SEED-11B) ni asientos retroactivos.
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
}
