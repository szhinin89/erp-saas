using ERP.Application.Modules.DocTypes.Services;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.DocTypes.Enums;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Services;

/// <summary>Ver <see cref="IDocumentFlowPolicyService"/>. Sin fila explícita en <c>document_flow_policy</c> para la company + tipo consultados, falla explícitamente — nunca asume un default.</summary>
public sealed class DocumentFlowPolicyService : IDocumentFlowPolicyService
{
    private readonly ErpDbContext _db;

    public DocumentFlowPolicyService(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<DocumentFlowPolicyResult> GetRequiredAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await _db
            .DocumentFlowPolicies.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.DocumentTypeCode == documentTypeCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is null)
            throw DocumentFlowPolicyViolationException.NotConfigured(documentTypeCode);

        return new DocumentFlowPolicyResult(
            policy.DocumentTypeCode,
            policy.IsActive,
            policy.CreationMode,
            policy.ConfirmationMode,
            policy.AuthorizationMode,
            policy.PendingDocumentMode,
            policy.CancellationMode,
            policy.RequiresCancellationReason,
            policy.RequiresAttachment,
            policy.RequiresSupplier,
            policy.RequiresDueDate,
            policy.PayableGenerationMode,
            policy.AccountingPostingMode,
            policy.InventoryImpactMode,
            policy.NotificationMode
        );
    }

    public async Task EnsureDraftCreationAllowedAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await GetRequiredAsync(companyId, documentTypeCode, cancellationToken);
        if (!policy.IsActive)
            throw DocumentFlowPolicyViolationException.DocumentTypeDisabled(documentTypeCode);
        if (policy.CreationMode == CreationMode.DirectCreation)
            throw DocumentFlowPolicyViolationException.DraftNotAllowed(documentTypeCode);
    }

    public async Task EnsureDirectCreationAllowedAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await GetRequiredAsync(companyId, documentTypeCode, cancellationToken);
        if (!policy.IsActive)
            throw DocumentFlowPolicyViolationException.DocumentTypeDisabled(documentTypeCode);
        if (policy.CreationMode == CreationMode.DraftRequired)
            throw DocumentFlowPolicyViolationException.DraftRequired(documentTypeCode);
    }

    public async Task<DocumentFlowPolicyResult> EnsureConfirmationFlowAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await GetRequiredAsync(companyId, documentTypeCode, cancellationToken);
        if (!policy.IsActive)
            throw DocumentFlowPolicyViolationException.DocumentTypeDisabled(documentTypeCode);
        if (policy.AuthorizationMode != AuthorizationMode.None)
            throw DocumentFlowPolicyViolationException.AuthorizationRequired();
        return policy;
    }

    public async Task<DocumentFlowPolicyResult> EnsureCancellationFlowAsync(
        Guid companyId,
        string documentTypeCode,
        string? reason,
        CancellationToken cancellationToken = default
    )
    {
        var policy = await GetRequiredAsync(companyId, documentTypeCode, cancellationToken);
        if (policy.CancellationMode == CancellationMode.NotAllowed)
            throw DocumentFlowPolicyViolationException.CancellationNotAllowed();
        if (policy.RequiresCancellationReason && string.IsNullOrWhiteSpace(reason))
            throw DocumentFlowPolicyViolationException.CancellationReasonRequired();
        return policy;
    }
}
