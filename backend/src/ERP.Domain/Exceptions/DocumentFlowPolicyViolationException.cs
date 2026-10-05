namespace ERP.Domain.Exceptions;

/// <summary>
/// Se lanza cuando una operación viola la política de flujo documental
/// (<c>ERP.Domain.Modules.DocTypes.Entities.DocumentFlowPolicy</c>) de una company — nunca un
/// permiso de usuario, que se valida por separado y antes de llegar aquí. Regla de negocio:
/// subclase de <see cref="DomainRuleViolationException"/> (→ DOMAIN_RULE_VIOLATION, 422).
/// </summary>
public sealed class DocumentFlowPolicyViolationException : DomainRuleViolationException
{
    public string Code { get; }

    private DocumentFlowPolicyViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public static DocumentFlowPolicyViolationException NotConfigured(string documentTypeCode) =>
        new(
            "document_flow_policy.not_configured",
            "La política de flujo documental no está configurada para este tipo de documento."
        );

    public static DocumentFlowPolicyViolationException DocumentTypeDisabled(
        string documentTypeCode
    ) =>
        new(
            "document_flow_policy.document_type_disabled",
            $"El tipo de documento '{documentTypeCode}' no está habilitado para esta empresa."
        );

    public static DocumentFlowPolicyViolationException DraftNotAllowed(string documentTypeCode) =>
        new(
            "document_flow_policy.draft_not_allowed",
            $"El tipo de documento '{documentTypeCode}' no admite borrador."
        );

    public static DocumentFlowPolicyViolationException DraftRequired(string documentTypeCode) =>
        new(
            "document_flow_policy.draft_required",
            $"El tipo de documento '{documentTypeCode}' requiere guardarse como borrador antes de confirmarse."
        );

    public static DocumentFlowPolicyViolationException AuthorizationRequired() =>
        new(
            "document_flow_policy.authorization_required",
            "La política de flujo documental requiere autorización antes de confirmar este documento."
        );

    public static DocumentFlowPolicyViolationException CancellationNotAllowed() =>
        new(
            "document_flow_policy.cancellation_not_allowed",
            "La política de flujo documental no permite anular este tipo de documento."
        );

    public static DocumentFlowPolicyViolationException CancellationReasonRequired() =>
        new(
            "document_flow_policy.cancellation_reason_required",
            "El motivo de anulación es obligatorio según la política de flujo documental."
        );
}
