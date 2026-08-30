using ERP.Application.Modules.DocTypes.Services;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Enums;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Services;

/// <summary>
/// Ver <see cref="IDocWorkflowPolicyService"/>. Sin fila explícita en <c>doc_workflow_policy</c>
/// para la company + doc type consultados, resuelve el default legado: habilitado, sin borrador
/// (GASDOC es la única excepción con borrador opcional — ver
/// <c>DocWorkflowPolicyBootstrapStep</c>, misma regla replicada aquí para companies sin backfill
/// todavía), confirmación directa — igual al comportamiento antes de esta entidad.
/// </summary>
public sealed class DocWorkflowPolicyService : IDocWorkflowPolicyService
{
    private readonly ErpDbContext _db;

    public DocWorkflowPolicyService(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<DocWorkflowPolicyResult> GetPolicyAsync(
        Guid companyId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await _db
            .DocWorkflowPolicies.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.DocTypeCode == docTypeCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is not null)
            return new DocWorkflowPolicyResult(
                docTypeCode,
                policy.IsEnabled,
                policy.DraftMode,
                policy.DefaultAction
            );

        var draftMode =
            docTypeCode == DocTypeCodes.ExpenseDocument ? DraftMode.Optional : DraftMode.Disabled;
        return new DocWorkflowPolicyResult(
            docTypeCode,
            IsEnabled: true,
            draftMode,
            DocWorkflowDefaultAction.Confirm
        );
    }

    public async Task ValidateCreateDraftAsync(
        Guid companyId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await GetPolicyAsync(companyId, docTypeCode, cancellationToken);
        if (!policy.IsEnabled)
            throw DocWorkflowPolicyViolationException.DocTypeDisabled(docTypeCode);
        if (policy.DraftMode == DraftMode.Disabled)
            throw DocWorkflowPolicyViolationException.DraftNotAllowed(docTypeCode);
    }

    public async Task ValidateCreateConfirmedAsync(
        Guid companyId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await GetPolicyAsync(companyId, docTypeCode, cancellationToken);
        if (!policy.IsEnabled)
            throw DocWorkflowPolicyViolationException.DocTypeDisabled(docTypeCode);
        if (policy.DraftMode == DraftMode.Required)
            throw DocWorkflowPolicyViolationException.DraftRequired(docTypeCode);
    }
}
