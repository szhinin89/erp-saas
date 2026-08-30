using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// DOC-TYPE-SSOT-01: <see cref="DocWorkflowPolicyBootstrapStep"/> (registrado en
/// <c>CompanyBootstrapOrchestrator</c>) solo corre para empresas NUEVAS. Este servicio cierra esa
/// brecha exclusivamente fuera de Production, siguiendo el mismo patrón de
/// <see cref="ExpensesCatalogBackfillService"/>: en cada arranque de la API, para cada company
/// activa aplica el mismo step (reutilizado, no duplicado — ya es idempotente por DocType). Solo
/// crea filas de política faltantes; nunca modifica una política ya existente.
/// </summary>
public sealed partial class DocWorkflowPolicyBackfillService
{
    private readonly ErpDbContext _db;
    private readonly IHostEnvironment _environment;
    private readonly DocWorkflowPolicyBootstrapStep _docWorkflowPolicyBootstrapStep;
    private readonly ILogger<DocWorkflowPolicyBackfillService> _logger;

    public DocWorkflowPolicyBackfillService(
        ErpDbContext db,
        IHostEnvironment environment,
        DocWorkflowPolicyBootstrapStep docWorkflowPolicyBootstrapStep,
        ILogger<DocWorkflowPolicyBackfillService> logger
    )
    {
        _db = db;
        _environment = environment;
        _docWorkflowPolicyBootstrapStep = docWorkflowPolicyBootstrapStep;
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
        // request/usuario real — mismo criterio ya usado en ExpensesCatalogBackfillService.
        var systemActorId = Guid.NewGuid();

        foreach (var company in activeCompanies)
        {
            using var _ = JobExecutionContext.Begin(company.TenantId, company.Id);
            await _docWorkflowPolicyBootstrapStep.ExecuteAsync(
                new CompanyBootstrapContext(company.TenantId, company.Id, systemActorId),
                cancellationToken
            );
        }

        LogBackfillCompleted();
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "DocWorkflowPolicy backfill checked for all active companies."
    )]
    private partial void LogBackfillCompleted();
}
