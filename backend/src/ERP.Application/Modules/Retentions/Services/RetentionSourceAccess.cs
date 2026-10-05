using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// ADR-036 (D-9) — la autorización sobre una retención se deriva de su documento ORIGEN (no existe
/// un permiso transversal de Retenciones): Compra → permisos de Compras, Gasto → permisos de Gastos.
/// Como el origen es un dato (<see cref="RetentionSourceDocumentType"/>), la decisión es server-side
/// en el handler, nunca un <c>[Authorize]</c> estático. <c>Manual</c> (reservado, sin flujo) se
/// deniega siempre — fail-closed.
/// </summary>
public interface IRetentionSourceAccess
{
    /// <summary>
    /// La retención del tenant y empresa activos, solo si el usuario puede ver su documento origen;
    /// <c>null</c> en cualquier otro caso (inexistente, otra empresa o sin permiso: indistinguibles).
    /// </summary>
    Task<RetentionDocument?> FindViewableAsync(Guid retentionId, CancellationToken ct = default);

    /// <summary>Lectura (XML/RIDE): Compra → <c>purchases.view</c>; Gasto → <c>expenses.documents.view</c>.</summary>
    Task<bool> CanViewAsync(RetentionSourceDocumentType sourceType, CancellationToken ct = default);

    /// <summary>Acción de recuperación: Compra → <c>purchases.update</c>; Gasto → <c>expenses.documents.confirm</c>.</summary>
    Task<bool> CanOperateAsync(RetentionSourceDocumentType sourceType, CancellationToken ct = default);

    /// <summary>
    /// ZH-RETENTION-SRI-ANNULMENT-01/01B — iniciar, registrar la presentación, verificar en el SRI,
    /// completar la finalización o desistir de una anulación SRI: el permiso EXISTENTE de anular el origen
    /// (Compra → <c>purchases.update</c>, el que protege <c>POST /purchases/{id}/cancel</c>; Gasto →
    /// <c>expenses.documents.cancel</c>). 01B retiró la resolución manual (y con ella el permiso reforzado
    /// <c>electronic-documents.retry</c>): el estado fiscal lo informa el SRI, nunca el usuario.
    /// </summary>
    Task<bool> CanCancelOriginAsync(RetentionSourceDocumentType sourceType, CancellationToken ct = default);
}

public sealed class RetentionSourceAccess : IRetentionSourceAccess
{
    private readonly IRuntimePermissionAuthorizer _authorizer;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly IRetentionDocumentRepository _retentions;

    public RetentionSourceAccess(
        IRuntimePermissionAuthorizer authorizer,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        IRetentionDocumentRepository retentions
    )
    {
        _authorizer = authorizer;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _retentions = retentions;
    }

    public async Task<RetentionDocument?> FindViewableAsync(
        Guid retentionId,
        CancellationToken ct = default
    )
    {
        var retention = await _retentions.GetByIdAsync(_currentTenant.TenantId, retentionId, ct);
        if (retention is null || retention.CompanyId != _currentCompany.CompanyId)
            return null;
        return await CanViewAsync(retention.SourceDocumentType, ct) ? retention : null;
    }

    public Task<bool> CanViewAsync(
        RetentionSourceDocumentType sourceType,
        CancellationToken ct = default
    ) =>
        IsAuthorizedAsync(
            sourceType switch
            {
                RetentionSourceDocumentType.PurchaseInvoice => PurchasePermissions.View,
                RetentionSourceDocumentType.ExpenseDocument => ExpensePermissions.DocumentsView,
                _ => null,
            },
            ct
        );

    public Task<bool> CanOperateAsync(
        RetentionSourceDocumentType sourceType,
        CancellationToken ct = default
    ) =>
        IsAuthorizedAsync(
            sourceType switch
            {
                RetentionSourceDocumentType.PurchaseInvoice => PurchasePermissions.Update,
                RetentionSourceDocumentType.ExpenseDocument => ExpensePermissions.DocumentsConfirm,
                _ => null,
            },
            ct
        );

    public Task<bool> CanCancelOriginAsync(
        RetentionSourceDocumentType sourceType,
        CancellationToken ct = default
    ) =>
        IsAuthorizedAsync(
            sourceType switch
            {
                RetentionSourceDocumentType.PurchaseInvoice => PurchasePermissions.Update,
                RetentionSourceDocumentType.ExpenseDocument => ExpensePermissions.DocumentsCancel,
                _ => null,
            },
            ct
        );

    private async Task<bool> IsAuthorizedAsync(string? permission, CancellationToken ct) =>
        permission is not null
        && _currentUser.UserId != Guid.Empty
        && await _authorizer.IsAuthorizedAsync(
            permission,
            _currentUser.UserId,
            _currentUser.Role ?? string.Empty,
            ct
        );
}
