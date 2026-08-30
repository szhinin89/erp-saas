using ERP.Domain.Common;
using ERP.Domain.Modules.DocTypes.Enums;

namespace ERP.Domain.Modules.DocTypes.Entities;

/// <summary>
/// DOCUMENT-FLOW-POLICY-01: política por company de CÓMO se comporta un
/// <see cref="DocType"/> a lo largo de su ciclo de vida — creación, confirmación,
/// autorización, anulación y los efectos que dispara (CxP, asiento contable, inventario,
/// notificaciones). Una fila = una company + un doc type.
///
/// Separación conceptual obligatoria (no debe romperse nunca):
/// - Los <b>permisos</b> (<c>ExpensePermissions</c>, etc.) definen QUIÉN puede ejecutar una acción.
/// - Esta política define CÓMO se comporta el documento — nunca reemplaza ni sustituye un permiso.
/// - Los estados del documento (<c>ExpenseStatus</c>, etc.) definen EN QUÉ ETAPA está un documento concreto.
/// - Los eventos de dominio (<c>ExpenseDocumentConfirmedEvent</c>, etc.) definen QUÉ efectos genera al avanzar.
///
/// Reemplaza a la entidad <c>DocWorkflowPolicy</c> (DOC-TYPE-SSOT-01) — mismo propósito, modelo
/// más rico. Sin fila explícita, el servicio de aplicación (<c>IDocumentFlowPolicyService</c>)
/// falla con un mensaje explícito de política no configurada — nunca asume un default.
/// </summary>
public sealed class DocumentFlowPolicy : AuditableEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public string DocumentTypeCode { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public CreationMode CreationMode { get; private set; }
    public ConfirmationMode ConfirmationMode { get; private set; }
    public AuthorizationMode AuthorizationMode { get; private set; }
    public PendingDocumentMode PendingDocumentMode { get; private set; }
    public CancellationMode CancellationMode { get; private set; }

    public bool RequiresCancellationReason { get; private set; }
    public bool RequiresAttachment { get; private set; }
    public bool RequiresSupplier { get; private set; }
    public bool RequiresDueDate { get; private set; }

    public PayableGenerationMode PayableGenerationMode { get; private set; }
    public AccountingPostingMode AccountingPostingMode { get; private set; }
    public InventoryImpactMode InventoryImpactMode { get; private set; }
    public NotificationMode NotificationMode { get; private set; }

    private DocumentFlowPolicy() { }

    public static DocumentFlowPolicy Create(
        Guid tenantId,
        Guid companyId,
        string documentTypeCode,
        bool isActive,
        CreationMode creationMode,
        ConfirmationMode confirmationMode,
        AuthorizationMode authorizationMode,
        PendingDocumentMode pendingDocumentMode,
        CancellationMode cancellationMode,
        bool requiresCancellationReason,
        bool requiresAttachment,
        bool requiresSupplier,
        bool requiresDueDate,
        PayableGenerationMode payableGenerationMode,
        AccountingPostingMode accountingPostingMode,
        InventoryImpactMode inventoryImpactMode,
        NotificationMode notificationMode,
        Guid createdBy
    )
    {
        var policy = new DocumentFlowPolicy
        {
            TenantId = tenantId,
            CompanyId = companyId,
            DocumentTypeCode = documentTypeCode,
            IsActive = isActive,
            CreationMode = creationMode,
            ConfirmationMode = confirmationMode,
            AuthorizationMode = authorizationMode,
            PendingDocumentMode = pendingDocumentMode,
            CancellationMode = cancellationMode,
            RequiresCancellationReason = requiresCancellationReason,
            RequiresAttachment = requiresAttachment,
            RequiresSupplier = requiresSupplier,
            RequiresDueDate = requiresDueDate,
            PayableGenerationMode = payableGenerationMode,
            AccountingPostingMode = accountingPostingMode,
            InventoryImpactMode = inventoryImpactMode,
            NotificationMode = notificationMode,
        };
        policy.SetCreated(createdBy);
        return policy;
    }

    public void Update(
        bool isActive,
        CreationMode creationMode,
        ConfirmationMode confirmationMode,
        AuthorizationMode authorizationMode,
        PendingDocumentMode pendingDocumentMode,
        CancellationMode cancellationMode,
        bool requiresCancellationReason,
        bool requiresAttachment,
        bool requiresSupplier,
        bool requiresDueDate,
        PayableGenerationMode payableGenerationMode,
        AccountingPostingMode accountingPostingMode,
        InventoryImpactMode inventoryImpactMode,
        NotificationMode notificationMode,
        Guid updatedBy
    )
    {
        IsActive = isActive;
        CreationMode = creationMode;
        ConfirmationMode = confirmationMode;
        AuthorizationMode = authorizationMode;
        PendingDocumentMode = pendingDocumentMode;
        CancellationMode = cancellationMode;
        RequiresCancellationReason = requiresCancellationReason;
        RequiresAttachment = requiresAttachment;
        RequiresSupplier = requiresSupplier;
        RequiresDueDate = requiresDueDate;
        PayableGenerationMode = payableGenerationMode;
        AccountingPostingMode = accountingPostingMode;
        InventoryImpactMode = inventoryImpactMode;
        NotificationMode = notificationMode;
        SetUpdated(updatedBy);
    }
}
