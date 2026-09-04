using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Services;
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
///
/// RETENTIONS-POSTING-RULE-SEED-01H: el filtro "sin reglas de contabilización" seguía siendo
/// "¿tiene AL MENOS UNA PostingRule?" — una company que ya tenía, por ejemplo,
/// Expenses/Purchases/Sales/Payables sembradas en una corrida anterior nunca volvía a calificar
/// para backfill, aun cuando esta misma fase agrega una regla nueva (Retentions/DocumentIssued)
/// que esa company todavía no tiene. Se reemplaza por una comprobación precisa contra
/// <see cref="AccountingBootstrapStep.RequiredPostingRuleKeys"/> (todas las claves
/// (SourceModule, FactType) configuradas hoy) — cualquier company a la que le falte una sola de
/// esas reglas vuelve a calificar. Nunca deja de detectar lo que el filtro anterior ya detectaba
/// (compañías sin ninguna regla), solo agrega precisión para reglas nuevas agregadas después del
/// primer seed de una company.
///
/// RETENTIONS-TAX-COMPONENT-POSTING-02C: la comprobación de 01H seguía siendo por CLAVE
/// (SourceModule, FactType) únicamente — una company con "Retentions"/"DocumentIssued" ya sembrada
/// (aunque fuera con la forma vieja de 2 líneas de 01H) pasaba el chequeo ("la clave existe") y
/// nunca volvía a calificar, aun cuando esta fase amplía esa misma regla a 3 líneas. Se agrega una
/// segunda comprobación, por CONTEO de líneas, contra
/// <see cref="AccountingBootstrapStep.RequiredPostingRuleLineCounts"/> — cualquier company cuya
/// regla exista pero con menos líneas de las que su forma vigente declara hoy también califica.
/// Mismo criterio que la comprobación anterior: nunca deja de detectar nada que ya detectaba, solo
/// agrega precisión un nivel más profundo (no solo "¿existe la regla?", también "¿está completa?").
/// La corrección real de las líneas la aplica
/// <see cref="AccountingBootstrapStep.TryCorrectLegacyRetentionsDocumentIssuedRule"/>, invocado
/// desde <see cref="AccountingBootstrapStep.ExecuteAsync"/> (reutilizado tal cual, no duplicado).
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
            var existingRules = await _db
                .PostingRules.IgnoreQueryFilters()
                .Where(r => r.TenantId == company.TenantId && r.CompanyId == company.Id)
                .Select(r => new
                {
                    r.SourceModule,
                    r.FactType,
                    LineCount = r.Lines.Count,
                })
                .ToListAsync(cancellationToken);
            var existingRuleLineCounts = existingRules.ToDictionary(
                r => (r.SourceModule, r.FactType),
                r => r.LineCount
            );
            // RETENTIONS-TAX-COMPONENT-POSTING-02C — no basta con "¿existe la clave?"
            // (existingRuleLineCounts.ContainsKey): una regla existente con menos líneas de las
            // que RequiredPostingRuleLineCounts espera para esa clave también necesita backfill
            // (ver doc comment de la clase). Una clave ausente cuenta como 0 líneas, así que esta
            // sola comprobación cubre ambos casos: regla completamente faltante y regla incompleta.
            var hasAllPostingRules = AccountingBootstrapStep.RequiredPostingRuleLineCounts.All(
                required =>
                    existingRuleLineCounts.TryGetValue(required.Key, out var actualLineCount)
                    && actualLineCount >= required.Value
            );

            if (!hasAllRetailAccounts || !hasAllPostingRules)
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

        // ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 3: corrige ParentAccountId de cuentas ya
        // existentes (no solo las recién creadas arriba) para TODAS las companies activas, no
        // solo las que tenían cuentas/reglas faltantes — una company con las 92/102 cuentas
        // completas puede igual tener ParentAccountId legacy/desalineado del código (ver
        // AccountHierarchyDiagnostics). Reutiliza el mismo actor de sistema.
        foreach (var company in activeCompanies)
        {
            using var _ = JobExecutionContext.Begin(company.TenantId, company.Id);
            await BackfillHierarchyAsync(company.TenantId, company.Id, systemActorId, cancellationToken);
        }
    }

    /// <summary>
    /// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 3: para una Company, alinea
    /// <see cref="Account.ParentAccountId"/> de cada cuenta existente con el padre canónico
    /// implicado por su código (<see cref="AccountHierarchyRules.GetExpectedParentCode"/>). Nunca
    /// crea cuentas (eso es responsabilidad de <see cref="AccountingBootstrapStep"/>, ya corrido
    /// arriba para las companies que lo necesitaban) ni cambia Code/Name — solo repara el enlace
    /// padre/hija. Si el padre canónico no existe en el Plan de Cuentas de esta company (cuenta
    /// custom del usuario con un código que no sigue el blueprint retail), no toca nada y lo
    /// reporta como inconsistencia pendiente vía log — nunca inventa una cuenta agrupadora fuera
    /// del blueprint aprobado. Idempotente: correrlo dos veces seguidas no cambia nada la segunda vez.
    /// </summary>
    public async Task<AccountHierarchyBackfillResult> BackfillHierarchyAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        CancellationToken cancellationToken = default
    )
    {
        var accounts = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var byCode = accounts.ToDictionary(a => a.Code.Value, StringComparer.Ordinal);
        var fixedCount = 0;
        var unresolvedCount = 0;

        foreach (var account in accounts)
        {
            var expectedParentCode = AccountHierarchyRules.GetExpectedParentCode(account.Code.Value);

            if (expectedParentCode is null)
            {
                if (account.ParentAccountId is not null)
                {
                    account.UpdateParent(null, actorId);
                    fixedCount++;
                }
                continue;
            }

            if (!byCode.TryGetValue(expectedParentCode, out var expectedParent))
            {
                unresolvedCount++;
                LogUnresolvedParent(account.Code.Value, expectedParentCode, companyId);
                continue;
            }

            if (account.ParentAccountId != expectedParent.Id)
            {
                account.UpdateParent(expectedParent.Id, actorId);
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            LogHierarchyFixed(fixedCount, companyId);
        }

        if (unresolvedCount > 0)
            LogHierarchyUnresolved(unresolvedCount, companyId);

        return new AccountHierarchyBackfillResult(accounts.Count, fixedCount, unresolvedCount);
    }

    /// <summary>
    /// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 8: diagnóstico de solo lectura (sin escribir
    /// nada) para una Company — carga cuentas y PostingRules y corre
    /// <see cref="AccountHierarchyDiagnostics.Analyze"/>. Base de <see cref="RunControlledHierarchyMaintenanceAsync"/>
    /// (antes/después) y de cualquier auditoría manual previa a decidir si vale la pena correr el
    /// backfill contra una company puntual.
    /// </summary>
    public async Task<AccountHierarchyReport> DiagnoseHierarchyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    )
    {
        var accounts = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .ToListAsync(cancellationToken);
        var postingRules = await _db
            .PostingRules.IgnoreQueryFilters()
            .Include(r => r.Lines)
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        return AccountHierarchyDiagnostics.Analyze(accounts, postingRules);
    }

    /// <summary>
    /// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: ejecución controlada y explícita del backfill de
    /// jerarquía para TODAS las companies activas, pensada para correr una sola vez contra
    /// Production vía <c>dotnet run -- backfill-accounting-chart-hierarchy</c> (mismo patrón ya
    /// usado por <c>backfill-master-data-classifications</c> en Program.cs) — nunca se invoca
    /// automáticamente en el arranque normal de la API, ni siquiera fuera de Production; el guard
    /// de <see cref="EnsureAsync"/> (<c>IsProduction()</c>) queda intacto. Por cada company: (1)
    /// diagnóstico previo de solo lectura; (2) si no hay hallazgos, no toca nada; (3) si hay
    /// hallazgos, envuelve el fix en una transacción explícita (rollback si algo falla) y corre un
    /// diagnóstico posterior para reportar el resultado real. Nunca crea cuentas ni cambia Code/Name
    /// — mismo alcance que <see cref="BackfillHierarchyAsync"/>.
    /// </summary>
    public async Task<AccountingHierarchyMaintenanceSummary> RunControlledHierarchyMaintenanceAsync(
        CancellationToken cancellationToken = default
    )
    {
        var activeCompanies = await _db
            .Companies.IgnoreQueryFilters()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.TenantId })
            .ToListAsync(cancellationToken);

        var systemActorId = Guid.NewGuid();
        var perCompanyResults = new List<AccountingHierarchyMaintenanceCompanyResult>();

        foreach (var company in activeCompanies)
        {
            using var _ = JobExecutionContext.Begin(company.TenantId, company.Id);

            var before = await DiagnoseHierarchyAsync(company.TenantId, company.Id, cancellationToken);
            LogDiagnosticBefore(company.Id, before.Issues.Count);

            if (before.Issues.Count == 0)
            {
                perCompanyResults.Add(
                    new AccountingHierarchyMaintenanceCompanyResult(company.Id, 0, 0, 0, 0)
                );
                continue;
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await BackfillHierarchyAsync(
                    company.TenantId,
                    company.Id,
                    systemActorId,
                    cancellationToken
                );
                await transaction.CommitAsync(cancellationToken);

                var after = await DiagnoseHierarchyAsync(company.TenantId, company.Id, cancellationToken);
                LogDiagnosticAfter(company.Id, after.Issues.Count);

                perCompanyResults.Add(
                    new AccountingHierarchyMaintenanceCompanyResult(
                        company.Id,
                        before.Issues.Count,
                        after.Issues.Count,
                        result.FixedParentCount,
                        result.UnresolvedParentCount
                    )
                );
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return new AccountingHierarchyMaintenanceSummary(perCompanyResults);
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

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Fixed ParentAccountId on {Count} accounts for company {CompanyId} to match their code's canonical parent."
    )]
    private partial void LogHierarchyFixed(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Account code {Code} implies parent code {ExpectedParentCode}, but no such account exists "
            + "in company {CompanyId} — left untouched (not part of the approved blueprint)."
    )]
    private partial void LogUnresolvedParent(string code, string expectedParentCode, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Count} accounts in company {CompanyId} have a code implying a parent that does not exist. See prior warnings for details."
    )]
    private partial void LogHierarchyUnresolved(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[Controlled hierarchy maintenance] Company {CompanyId}: {IssueCount} hierarchy issue(s) found before backfill."
    )]
    private partial void LogDiagnosticBefore(Guid companyId, int issueCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[Controlled hierarchy maintenance] Company {CompanyId}: {IssueCount} hierarchy issue(s) remain after backfill."
    )]
    private partial void LogDiagnosticAfter(Guid companyId, int issueCount);
}

/// <summary>
/// Resultado de <see cref="AccountingChartBackfillService.BackfillHierarchyAsync"/> — usado por
/// la auditoría antes/después (Fase 8) y por tests de idempotencia.
/// </summary>
public sealed record AccountHierarchyBackfillResult(
    int TotalAccounts,
    int FixedParentCount,
    int UnresolvedParentCount
);

/// <summary>
/// Resultado de <see cref="AccountingChartBackfillService.RunControlledHierarchyMaintenanceAsync"/>
/// — un renglón por company activa, para imprimir en consola desde el comando CLI de
/// <c>Program.cs</c>.
/// </summary>
public sealed record AccountingHierarchyMaintenanceCompanyResult(
    Guid CompanyId,
    int IssuesBefore,
    int IssuesAfter,
    int FixedParentCount,
    int UnresolvedParentCount
);

public sealed record AccountingHierarchyMaintenanceSummary(
    IReadOnlyList<AccountingHierarchyMaintenanceCompanyResult> Companies
)
{
    public int TotalCompanies => Companies.Count;
    public int CompaniesWithIssuesBefore => Companies.Count(c => c.IssuesBefore > 0);
    public int CompaniesWithIssuesAfter => Companies.Count(c => c.IssuesAfter > 0);
    public int TotalFixed => Companies.Sum(c => c.FixedParentCount);
    public int TotalUnresolved => Companies.Sum(c => c.UnresolvedParentCount);
}
