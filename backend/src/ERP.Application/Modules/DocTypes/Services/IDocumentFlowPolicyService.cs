using ERP.Domain.Modules.DocTypes.Enums;

namespace ERP.Application.Modules.DocTypes.Services;

/// <summary>
/// Snapshot de <c>DocumentFlowPolicy</c> resuelto para una company + tipo de documento. Describe
/// CÓMO se comporta el documento — nunca QUIÉN puede ejecutar una acción sobre él (eso es un
/// permiso, validado por separado antes de llegar a este servicio).
/// </summary>
public sealed record DocumentFlowPolicyResult(
    string DocumentTypeCode,
    bool IsActive,
    CreationMode CreationMode,
    ConfirmationMode ConfirmationMode,
    AuthorizationMode AuthorizationMode,
    PendingDocumentMode PendingDocumentMode,
    CancellationMode CancellationMode,
    bool RequiresCancellationReason,
    bool RequiresAttachment,
    bool RequiresSupplier,
    bool RequiresDueDate,
    PayableGenerationMode PayableGenerationMode,
    AccountingPostingMode AccountingPostingMode,
    InventoryImpactMode InventoryImpactMode,
    NotificationMode NotificationMode
);

/// <summary>
/// SSOT de aplicación para consultar y validar la política de FLUJO DOCUMENTAL de un tipo de
/// documento por company (DOCUMENT-FLOW-POLICY-01). No es una fuente de permisos — un handler que
/// use este servicio debe validar el permiso de la acción (p. ej. <c>expenses.documents.confirm</c>)
/// por separado, antes o después, nunca en reemplazo de esta validación.
/// </summary>
public interface IDocumentFlowPolicyService
{
    /// <summary>Lanza <c>DocumentFlowPolicyViolationException.NotConfigured</c> si no hay política para la company + tipo.</summary>
    Task<DocumentFlowPolicyResult> GetRequiredAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lanza si el tipo está deshabilitado, o si <see cref="CreationMode.DirectCreation"/> no admite crear como borrador.</summary>
    Task EnsureDraftCreationAllowedAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lanza si el tipo está deshabilitado, o si <see cref="CreationMode.DraftRequired"/> exige pasar por borrador antes de confirmarse.</summary>
    Task EnsureDirectCreationAllowedAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lanza si el tipo está deshabilitado, o si <see cref="AuthorizationMode"/> exige autorización
    /// previa a la confirmación. Devuelve la política resuelta para que el caller decida los efectos
    /// a disparar (CxP, asiento, inventario, notificaciones) según sus modos.
    /// </summary>
    Task<DocumentFlowPolicyResult> EnsureConfirmationFlowAsync(
        Guid companyId,
        string documentTypeCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lanza si <see cref="CancellationMode.NotAllowed"/>, o si <c>RequiresCancellationReason</c> es
    /// verdadero y <paramref name="reason"/> viene vacío. Devuelve la política resuelta para que el
    /// caller decida si debe reversar CxP/asiento (<see cref="CancellationMode.AllowedAfterConfirmationWithReversal"/>).
    /// </summary>
    Task<DocumentFlowPolicyResult> EnsureCancellationFlowAsync(
        Guid companyId,
        string documentTypeCode,
        string? reason,
        CancellationToken cancellationToken = default
    );
}
