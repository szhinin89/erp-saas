using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Una fila de <see cref="DocWorkflowPolicy"/> por cada <see cref="DocType"/> activo del catálogo
/// global, para la company nueva. GASDOC recibe borrador opcional (DOC-TYPE-SSOT-01); el resto
/// queda sin borrador, confirmación directa — igual al comportamiento existente antes de esta
/// entidad. No depende de ningún otro step. Idempotente.
/// </summary>
public sealed partial class DocWorkflowPolicyBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.DocWorkflowPolicy;

    private readonly ErpDbContext _db;
    private readonly ILogger<DocWorkflowPolicyBootstrapStep> _logger;

    public DocWorkflowPolicyBootstrapStep(ErpDbContext db, ILogger<DocWorkflowPolicyBootstrapStep> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;

        var docTypeCodes = await _db
            .DocTypes.Where(d => d.IsActive)
            .Select(d => d.Code)
            .ToListAsync(cancellationToken);

        foreach (var docTypeCode in docTypeCodes)
        {
            var exists = await _db
                .DocWorkflowPolicies.IgnoreQueryFilters()
                .AnyAsync(
                    p => p.TenantId == tenantId && p.CompanyId == companyId && p.DocTypeCode == docTypeCode,
                    cancellationToken
                );

            if (exists)
            {
                LogPolicySkipped(docTypeCode, companyId);
                continue;
            }

            var (draftMode, defaultAction) =
                docTypeCode == DocTypeCodes.ExpenseDocument
                    ? (DraftMode.Optional, DocWorkflowDefaultAction.Confirm)
                    : (DraftMode.Disabled, DocWorkflowDefaultAction.Confirm);

            var policy = DocWorkflowPolicy.Create(
                tenantId: tenantId,
                companyId: companyId,
                docTypeCode: docTypeCode,
                isEnabled: true,
                draftMode: draftMode,
                defaultAction: defaultAction,
                createdBy: actorId
            );

            _db.DocWorkflowPolicies.Add(policy);
            LogPolicySeeded(docTypeCode, companyId);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "DocWorkflowPolicy {DocTypeCode} already exists for company {CompanyId}. Skipping."
    )]
    private partial void LogPolicySkipped(string docTypeCode, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocWorkflowPolicy {DocTypeCode} seeded for company {CompanyId}."
    )]
    private partial void LogPolicySeeded(string docTypeCode, Guid companyId);
}
