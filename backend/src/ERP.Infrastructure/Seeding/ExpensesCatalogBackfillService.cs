using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// EXPENSES-CATALOG-BOOTSTRAP-09-FIX: <see cref="ExpensesCatalogBootstrapStep"/> (registrado en
/// <c>CompanyBootstrapOrchestrator</c>) solo corre para empresas NUEVAS. Este servicio cierra esa
/// brecha exclusivamente fuera de Production, siguiendo el mismo patrón de
/// <see cref="AccountingChartBackfillService"/>: en cada arranque de la API, para cada company
/// activa (1) crea el catálogo de gastos si falta (reutilizando
/// <see cref="ExpensesCatalogBootstrapStep.ExecuteAsync"/>, que ya es idempotente) y (2) corrige
/// el <c>AccountingAccountId</c> de subcategorías del Template que fueron sembradas con el mapeo
/// incorrecto anterior (vía <see cref="ExpensesCatalogBootstrapStep.CorrectAccountMappingsAsync"/>).
/// Nunca toca subcategorías personalizadas fuera del Template, nunca borra ni desactiva nodos.
/// Debe correr después de <see cref="AccountingChartBackfillService"/> porque depende de que las
/// cuentas de gasto ya existan.
/// </summary>
public sealed partial class ExpensesCatalogBackfillService
{
    private readonly ErpDbContext _db;
    private readonly IHostEnvironment _environment;
    private readonly ExpensesCatalogBootstrapStep _expensesCatalogBootstrapStep;
    private readonly ILogger<ExpensesCatalogBackfillService> _logger;

    public ExpensesCatalogBackfillService(
        ErpDbContext db,
        IHostEnvironment environment,
        ExpensesCatalogBootstrapStep expensesCatalogBootstrapStep,
        ILogger<ExpensesCatalogBackfillService> logger
    )
    {
        _db = db;
        _environment = environment;
        _expensesCatalogBootstrapStep = expensesCatalogBootstrapStep;
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

        // Actor de sistema: este backfill corre en el arranque de la API, fuera de cualquier
        // request/usuario real — mismo criterio ya usado en AccountingChartBackfillService.
        var systemActorId = Guid.NewGuid();

        foreach (var company in activeCompanies)
        {
            using var _ = JobExecutionContext.Begin(company.TenantId, company.Id);
            var context = new CompanyBootstrapContext(company.TenantId, company.Id, systemActorId);

            await _expensesCatalogBootstrapStep.ExecuteAsync(context, cancellationToken);
            var correctedCount = await _expensesCatalogBootstrapStep.CorrectAccountMappingsAsync(
                context,
                cancellationToken
            );

            if (correctedCount > 0)
                LogCompanyAccountMappingsCorrected(company.Id, correctedCount);
        }

        LogBackfillCompleted();
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Expenses catalog backfill checked for all active companies."
    )]
    private partial void LogBackfillCompleted();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Corrected {CorrectedCount} expense catalog account mappings for company {CompanyId}."
    )]
    private partial void LogCompanyAccountMappingsCorrected(Guid companyId, int correctedCount);
}
