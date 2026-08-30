using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Una fila de <see cref="DocumentFlowPolicy"/> por cada <see cref="DocType"/> activo del catálogo
/// global, para la company nueva. GASDOC (Gastos) recibe la política obligatoria de
/// DOCUMENT-FLOW-POLICY-01 (borrador requerido, confirmación manual, CxP y asiento al confirmar,
/// anulación con reversa); el resto queda con el default seguro heredado del comportamiento
/// existente antes de esta entidad (creación directa, sin efectos declarados vía política — sus
/// módulos, si los tienen, resuelven esos efectos por su cuenta). No depende de ningún otro step.
/// Idempotente.
/// </summary>
public sealed partial class DocumentFlowPolicyBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.DocumentFlowPolicy;

    private readonly ErpDbContext _db;
    private readonly ILogger<DocumentFlowPolicyBootstrapStep> _logger;

    public DocumentFlowPolicyBootstrapStep(ErpDbContext db, ILogger<DocumentFlowPolicyBootstrapStep> logger)
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
                .DocumentFlowPolicies.IgnoreQueryFilters()
                .AnyAsync(
                    p => p.TenantId == tenantId && p.CompanyId == companyId && p.DocumentTypeCode == docTypeCode,
                    cancellationToken
                );

            if (exists)
            {
                LogPolicySkipped(docTypeCode, companyId);
                continue;
            }

            var policy = docTypeCode == DocTypeCodes.ExpenseDocument
                ? BuildExpenseDocumentDefault(tenantId, companyId, actorId)
                : BuildLegacyDefault(tenantId, companyId, docTypeCode, actorId);

            _db.DocumentFlowPolicies.Add(policy);
            LogPolicySeeded(docTypeCode, companyId);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Política obligatoria inicial de DOCUMENT-FLOW-POLICY-01 para ExpenseDocument (GASDOC).</summary>
    private static DocumentFlowPolicy BuildExpenseDocumentDefault(Guid tenantId, Guid companyId, Guid actorId) =>
        DocumentFlowPolicy.Create(
            tenantId: tenantId,
            companyId: companyId,
            documentTypeCode: DocTypeCodes.ExpenseDocument,
            isActive: true,
            creationMode: CreationMode.DraftRequired,
            confirmationMode: ConfirmationMode.ManualConfirmation,
            authorizationMode: AuthorizationMode.None,
            pendingDocumentMode: PendingDocumentMode.None,
            cancellationMode: CancellationMode.AllowedAfterConfirmationWithReversal,
            requiresCancellationReason: true,
            requiresAttachment: false,
            requiresSupplier: true,
            requiresDueDate: true,
            payableGenerationMode: PayableGenerationMode.OnConfirmation,
            accountingPostingMode: AccountingPostingMode.OnConfirmation,
            inventoryImpactMode: InventoryImpactMode.None,
            notificationMode: NotificationMode.None,
            createdBy: actorId
        );

    /// <summary>Default seguro para tipos de documento sin política declarada explícitamente: igual al comportamiento existente antes de esta entidad (creación directa, sin efectos vía política).</summary>
    private static DocumentFlowPolicy BuildLegacyDefault(
        Guid tenantId,
        Guid companyId,
        string docTypeCode,
        Guid actorId
    ) =>
        DocumentFlowPolicy.Create(
            tenantId: tenantId,
            companyId: companyId,
            documentTypeCode: docTypeCode,
            isActive: true,
            creationMode: CreationMode.DirectCreation,
            confirmationMode: ConfirmationMode.AutoConfirmOnCreate,
            authorizationMode: AuthorizationMode.None,
            pendingDocumentMode: PendingDocumentMode.None,
            cancellationMode: CancellationMode.NotAllowed,
            requiresCancellationReason: false,
            requiresAttachment: false,
            requiresSupplier: false,
            requiresDueDate: false,
            payableGenerationMode: PayableGenerationMode.None,
            accountingPostingMode: AccountingPostingMode.None,
            inventoryImpactMode: InventoryImpactMode.None,
            notificationMode: NotificationMode.None,
            createdBy: actorId
        );

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "DocumentFlowPolicy {DocTypeCode} already exists for company {CompanyId}. Skipping."
    )]
    private partial void LogPolicySkipped(string docTypeCode, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "DocumentFlowPolicy {DocTypeCode} seeded for company {CompanyId}."
    )]
    private partial void LogPolicySeeded(string docTypeCode, Guid companyId);
}
