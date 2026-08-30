namespace ERP.Domain.Exceptions;

/// <summary>
/// Se lanza cuando una operación viola la política de flujo documental de
/// <c>ERP.Domain.Modules.DocTypes.Entities.DocWorkflowPolicy</c> para una company (tipo de
/// documento deshabilitado, o borrador solicitado sobre un tipo que no lo admite). Subclase de
/// <see cref="InvalidOperationException"/> para que <c>ExceptionMiddleware</c> la traduzca a HTTP
/// 422 sin requerir un caso nuevo — mismo criterio que <see cref="SystemSeededRecordException"/>.
/// </summary>
public sealed class DocWorkflowPolicyViolationException : InvalidOperationException
{
    public string Code { get; }

    private DocWorkflowPolicyViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public static DocWorkflowPolicyViolationException DocTypeDisabled(string docTypeCode) =>
        new(
            "doc_workflow.doc_type_disabled",
            $"El tipo de documento '{docTypeCode}' no está habilitado para esta empresa."
        );

    public static DocWorkflowPolicyViolationException DraftNotAllowed(string docTypeCode) =>
        new(
            "doc_workflow.draft_not_allowed",
            $"El tipo de documento '{docTypeCode}' no admite borrador."
        );

    public static DocWorkflowPolicyViolationException DraftRequired(string docTypeCode) =>
        new(
            "doc_workflow.draft_required",
            $"El tipo de documento '{docTypeCode}' requiere guardarse como borrador antes de confirmarse."
        );
}
