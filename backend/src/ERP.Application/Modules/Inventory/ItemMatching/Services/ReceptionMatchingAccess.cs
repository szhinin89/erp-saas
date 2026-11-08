using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Application.Modules.Inventory.ItemMatching.Services;

/// <summary>Resultado de <see cref="IReceptionMatchingAccess.DecideAsync"/>.</summary>
public enum ReceptionMatchingDecision
{
    Allowed,
    Forbidden,

    /// <summary>
    /// La compra del comprobante ya está confirmada: sus líneas de recepción y equivalencias no se
    /// modifican desde la conciliación (misma regla que ya aplicaba "Corregir relación").
    /// </summary>
    PurchaseConfirmed,
}

/// <summary>
/// PURCHASES-RECEPTION-WRITE-PERMISSIONS-01 (H3) — permiso de las cinco operaciones de conciliación
/// (vincular, desvincular, corregir, conciliar en lote y resolver líneas). Escriben solo líneas de la
/// recepción y equivalencias de código de proveedor, nunca la compra, pero se usan en dos contextos:
/// preparar una compra nueva o editar una ya registrada. El permiso se deriva del dato, server-side
/// (mismo patrón que <c>RetentionSourceAccess</c>, ADR-036 D-9): con una compra activa (Draft o
/// Confirmed) de la misma clave de acceso en la empresa → <c>purchases.update</c>; sin ella →
/// <c>purchases.create</c>. Ninguno de los dos sustituye al otro. Con la compra ya Confirmed, la
/// conciliación no modifica la trazabilidad de sus líneas XML (409), aunque el usuario tenga permiso.
/// </summary>
public interface IReceptionMatchingAccess
{
    /// <summary>
    /// Filtro previo a toda lectura: sin <c>purchases.create</c> ni <c>purchases.update</c> no hay
    /// contexto en el que conciliar (403 sin revelar si el documento existe).
    /// </summary>
    Task<bool> CanMatchInAnyContextAsync(CancellationToken ct = default);

    /// <summary>Decisión exacta para conciliar las líneas de <paramref name="document"/>.</summary>
    Task<ReceptionMatchingDecision> DecideAsync(
        PurchaseReceptionDocument document,
        CancellationToken ct = default
    );
}

public sealed class ReceptionMatchingAccess(
    IRuntimePermissionAuthorizer authorizer,
    ICurrentUser currentUser,
    IPurchaseInvoiceRepository purchases
) : IReceptionMatchingAccess
{
    public const string ForbiddenMessage =
        "No tiene permiso para conciliar las líneas de este comprobante.";

    public const string ConfirmedPurchaseMessage =
        "La compra de este comprobante ya está confirmada: sus líneas y equivalencias no se modifican desde aquí. Gestione los códigos del proveedor en la ficha del producto.";

    /// <summary>Traduce una decisión no permitida al resultado del handler (403 o 409).</summary>
    public static Result<T> Reject<T>(ReceptionMatchingDecision decision) =>
        decision == ReceptionMatchingDecision.PurchaseConfirmed
            ? Result<T>.Conflict(ConfirmedPurchaseMessage)
            : Result<T>.Forbidden(ForbiddenMessage);

    public async Task<bool> CanMatchInAnyContextAsync(CancellationToken ct = default) =>
        await IsAuthorizedAsync(PurchasePermissions.Create, ct)
        || await IsAuthorizedAsync(PurchasePermissions.Update, ct);

    public async Task<ReceptionMatchingDecision> DecideAsync(
        PurchaseReceptionDocument document,
        CancellationToken ct = default
    )
    {
        // GetByAccessKeyAsync: solo compras activas (Draft/Confirmed) de la empresa operativa.
        var existing = await purchases.GetByAccessKeyAsync(document.TenantId, document.AccessKey, ct);
        var permission = existing is null ? PurchasePermissions.Create : PurchasePermissions.Update;
        if (!await IsAuthorizedAsync(permission, ct))
            return ReceptionMatchingDecision.Forbidden;
        return existing is { Status: PurchaseStatus.Confirmed }
            ? ReceptionMatchingDecision.PurchaseConfirmed
            : ReceptionMatchingDecision.Allowed;
    }

    private async Task<bool> IsAuthorizedAsync(string permission, CancellationToken ct) =>
        currentUser.UserId != Guid.Empty
        && await authorizer.IsAuthorizedAsync(
            permission,
            currentUser.UserId,
            currentUser.Role ?? string.Empty,
            ct
        );
}
