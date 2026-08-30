using ERP.Domain.Modules.DocTypes.Enums;

namespace ERP.Application.Modules.DocTypes.Services;

/// <summary>
/// Resultado de resolver la política de flujo documental de un <c>DocType</c> para una company.
/// Cuando no existe fila explícita en <c>doc_workflow_policy</c>, la implementación resuelve un
/// default seguro compatible con el comportamiento previo a DOC-TYPE-SSOT-01 (habilitado, sin
/// borrador, confirmación directa), nunca falla ni bloquea el flujo existente.
/// </summary>
public sealed record DocWorkflowPolicyResult(
    string DocTypeCode,
    bool IsEnabled,
    DraftMode DraftMode,
    DocWorkflowDefaultAction DefaultAction
);

/// <summary>
/// SSOT de aplicación para consultar y validar la política de borrador/confirmación de un
/// <c>DocType</c> por company. No aplica todavía a ningún flujo de ventas/compras existente —
/// fase 1 de DOC-TYPE-SSOT-01 es lectura/seed; la integración a Expenses queda para una fase
/// posterior.
/// </summary>
public interface IDocWorkflowPolicyService
{
    Task<DocWorkflowPolicyResult> GetPolicyAsync(
        Guid companyId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lanza <c>DocWorkflowPolicyViolationException</c> si el tipo está deshabilitado o no admite borrador.</summary>
    Task ValidateCreateDraftAsync(
        Guid companyId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lanza <c>DocWorkflowPolicyViolationException</c> si el tipo está deshabilitado, o si
    /// <see cref="DraftMode.Required"/> exige pasar por borrador antes de confirmarse. Solo aplica
    /// a la creación de un documento ya confirmado (sin borrador previo) — confirmar un borrador
    /// ya existente (<c>ConfirmExpenseDocumentCommand</c> y equivalentes) nunca llama este método.
    /// </summary>
    Task ValidateCreateConfirmedAsync(
        Guid companyId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    );
}
