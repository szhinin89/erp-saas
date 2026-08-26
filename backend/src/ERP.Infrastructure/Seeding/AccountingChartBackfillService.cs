using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// ACCOUNTING-INITIAL-CHART-SEED-11 / ACCOUNTING-BASE-CHART-TEMPLATE-13:
/// <see cref="AccountingBootstrapStep"/> (registrado en <c>CompanyBootstrapOrchestrator</c>) solo
/// corre para empresas NUEVAS — <c>Company</c> existentes creadas antes de que este step existiera
/// nunca vuelven a pasar por el bootstrap de creación. Este servicio cierra esa brecha
/// exclusivamente fuera de Production: en cada arranque de la API, busca companies activas con
/// cuentas retail faltantes o sin reglas contables y les aplica el mismo step (reutilizado, no
/// duplicado). El step solo crea cuentas/reglas faltantes; nunca modifica cuentas existentes,
/// documentos operativos ni asientos.
///
/// ACCOUNTING-POSTING-RULES-SEED-11B: el filtro original ("sin ninguna cuenta") dejaba afuera a
/// companies que ya recibieron el backfill de cuentas en una corrida anterior de este mismo
/// servicio (p. ej. "ZH TECH", backfillada en ACCOUNTING-INITIAL-CHART-SEED-11) — esas nunca
/// volverían a pasar por <see cref="AccountingBootstrapStep"/> y por lo tanto nunca recibirían
/// las <c>PostingRule</c> nuevas de esta fase. Se amplía a "sin cuentas O sin reglas de
/// contabilización" — <see cref="AccountingBootstrapStep.ExecuteAsync"/> sigue siendo idempotente
/// por bloque (cuentas/período/reglas). ACCOUNTING-BASE-CHART-TEMPLATE-13 amplía el filtro: una
/// company con las 13 cuentas mínimas y reglas ya sembradas igualmente debe pasar si le faltan
/// cuentas de la nueva plantilla retail; las cuentas existentes quedan intactas.
/// </summary>
public sealed partial class AccountingChartBackfillService
{
    private readonly ErpDbContext _db;
    private readonly IHostEnvironment _environment;
    private readonly AccountingBootstrapStep _accountingBootstrapStep;
    private readonly ILogger<AccountingChartBackfillService> _logger;

    public AccountingChartBackfillService(
        ErpDbContext db,
        IHostEnvironment environment,
        AccountingBootstrapStep accountingBootstrapStep,
        ILogger<AccountingChartBackfillService> logger
    )
    {
        _db = db;
        _environment = environment;
        _accountingBootstrapStep = accountingBootstrapStep;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (_environment.IsProduction())
            return;

        var activeCompanies = await _db
            .Companies.IgnoreQueryFilters()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.TenantId })
            .ToListAsync(cancellationToken);

        var requiredAccountCodes = AccountingBootstrapStep.RequiredRetailAccountCodes;
        var companiesPendingBackfill = new List<(Guid Id, Guid TenantId)>();
        foreach (var company in activeCompanies)
        {
            var accountCodes = await _db
                .Accounts.IgnoreQueryFilters()
                .Where(a => a.TenantId == company.TenantId && a.CompanyId == company.Id)
                .Select(a => a.Code.Value)
                .ToListAsync(cancellationToken);
            var accountCodeSet = accountCodes.ToHashSet(StringComparer.Ordinal);
            var hasAllRetailAccounts = requiredAccountCodes.All(accountCodeSet.Contains);
            var hasPostingRules = await _db
                .PostingRules.IgnoreQueryFilters()
                .AnyAsync(
                    r => r.TenantId == company.TenantId && r.CompanyId == company.Id,
                    cancellationToken
                );

            if (!hasAllRetailAccounts || !hasPostingRules)
                companiesPendingBackfill.Add((company.Id, company.TenantId));
        }

        if (companiesPendingBackfill.Count == 0)
        {
            LogNoCompaniesToBackfill();
            return;
        }

        // Actor de sistema: este backfill corre en el arranque de la API, fuera de cualquier
        // request/usuario real — mismo criterio ya usado en CompanyProvisioningService para el
        // bootstrap de una company nueva.
        var systemActorId = Guid.NewGuid();

        foreach (var company in companiesPendingBackfill)
        {
            using var _ = JobExecutionContext.Begin(company.TenantId, company.Id);
            await _accountingBootstrapStep.ExecuteAsync(
                new CompanyBootstrapContext(company.TenantId, company.Id, systemActorId),
                cancellationToken
            );
            LogCompanyBackfilled(company.Id);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "All active companies already have the retail Accounting template. Nothing to backfill."
    )]
    private partial void LogNoCompaniesToBackfill();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Backfilled retail Accounting configuration (chart of accounts/period/posting rules) for company {CompanyId}."
    )]
    private partial void LogCompanyBackfilled(Guid companyId);
}
