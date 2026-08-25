using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// ACCOUNTING-INITIAL-CHART-SEED-11: <see cref="AccountingBootstrapStep"/> (registrado en
/// <c>CompanyBootstrapOrchestrator</c>) solo corre para empresas NUEVAS — <c>Company</c>
/// existentes creadas antes de que este step existiera (p. ej. "ZH TECH" en la base local, ver
/// diagnóstico ACCOUNTING-DATA-SEED-AND-SMOKE-10G) nunca vuelven a pasar por el bootstrap de
/// creación. Este servicio cierra esa brecha exclusivamente fuera de Production: en cada arranque
/// de la API, busca companies activas sin ninguna cuenta contable y les aplica el mismo step
/// (reutilizado, no duplicado) — genérico por Company, nunca hardcodeado a un CompanyId
/// específico. Idempotente por construcción (el step ya solo crea lo que falta); no toca
/// documentos operativos ni crea asientos. Nunca se ejecuta en Production — a diferencia de
/// <see cref="E2E.E2ESeedService"/> no requiere una bandera adicional porque es puramente
/// aditivo (nunca crea usuarios/tenants/companies, solo completa Accounting de companies que ya
/// existen), pero comparte el mismo gate de entorno por seguridad.
///
/// ACCOUNTING-POSTING-RULES-SEED-11B: el filtro original ("sin ninguna cuenta") dejaba afuera a
/// companies que ya recibieron el backfill de cuentas en una corrida anterior de este mismo
/// servicio (p. ej. "ZH TECH", backfillada en ACCOUNTING-INITIAL-CHART-SEED-11) — esas nunca
/// volverían a pasar por <see cref="AccountingBootstrapStep"/> y por lo tanto nunca recibirían
/// las <c>PostingRule</c> nuevas de esta fase. Se amplía a "sin cuentas O sin reglas de
/// contabilización" — <see cref="AccountingBootstrapStep.ExecuteAsync"/> sigue siendo idempotente
/// por bloque (cuentas/período/reglas), así que una company con cuentas completas pero sin reglas
/// solo ejecuta el bloque de reglas, sin re-tocar cuentas/período ya sembrados.
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

        var companiesPendingBackfill = await _db
            .Companies.IgnoreQueryFilters()
            .Where(c =>
                c.IsActive
                && (
                    !_db.Accounts.Any(a => a.CompanyId == c.Id)
                    || !_db.PostingRules.Any(r => r.CompanyId == c.Id)
                )
            )
            .Select(c => new { c.Id, c.TenantId })
            .ToListAsync(cancellationToken);

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
        Message = "All active companies already have a chart of accounts. Nothing to backfill."
    )]
    private partial void LogNoCompaniesToBackfill();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Backfilled minimal Accounting configuration (chart of accounts/period/posting rules) for company {CompanyId}."
    )]
    private partial void LogCompanyBackfilled(Guid companyId);
}
