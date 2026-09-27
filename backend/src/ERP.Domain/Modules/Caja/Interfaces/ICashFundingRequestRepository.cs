using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Domain.Modules.Caja.Interfaces;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — persistencia de <see cref="CashFundingRequest"/>. Todas
/// las lecturas son de la empresa operativa (fail-closed). Orden único de locks:
/// <c>CashSession</c> → <c>CashFundingRequest</c> → locks financieros existentes.
/// </summary>
public interface ICashFundingRequestRepository
{
    Task<CashFundingRequest?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lock exclusivo oficial (<c>SELECT … FOR UPDATE</c>) sobre la solicitud dentro de la
    /// transacción ambiente, seguido de recarga (estado vigente bajo el lock). Se adquiere SIEMPRE
    /// después del lock de la <c>CashSession</c> objetivo.
    /// </summary>
    Task<CashFundingRequest?> GetByIdForUpdateAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>Idempotencia de la creación: solicitud ya registrada con este ClientRequestId (o null).</summary>
    Task<CashFundingRequest?> GetByClientRequestIdAsync(
        Guid tenantId,
        Guid clientRequestId,
        CancellationToken ct = default
    );

    /// <summary>Solicitudes de una sesión de caja en un estado (p. ej. pendientes que atiende el cajero).</summary>
    Task<IReadOnlyList<CashFundingRequest>> ListBySessionAsync(
        Guid tenantId,
        Guid cashSessionId,
        CashFundingRequestStatus status,
        CancellationToken ct = default
    );

    /// <summary>Solicitudes de un solicitante en un estado ("Mis solicitudes").</summary>
    Task<IReadOnlyList<CashFundingRequest>> ListByRequesterAsync(
        Guid tenantId,
        Guid requestedByUserId,
        CashFundingRequestStatus status,
        CancellationToken ct = default
    );

    /// <summary>
    /// ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — bloquea (FOR UPDATE, orden determinista por Id) y
    /// devuelve con tracking las solicitudes Pending de una sesión. Para el cierre de caja, que las
    /// cancela en su misma transacción DESPUÉS de bloquear la CashSession (orden único de locks).
    /// </summary>
    Task<IReadOnlyList<CashFundingRequest>> ListPendingBySessionForUpdateAsync(
        Guid tenantId,
        Guid cashSessionId,
        CancellationToken ct = default
    );

    /// <summary>
    /// ZH-CASH-FUNDING-REQUEST-API-02E-D — listado paginado de solo lectura (sin tracking) de la
    /// empresa operativa. <paramref name="branchId"/> acota la bandeja del cajero a la sucursal
    /// activa; <paramref name="requestedByUserId"/> acota "Mis solicitudes". Orden estable:
    /// <c>RequestedAtUtc</c> desc, <c>Id</c> desc.
    /// </summary>
    Task<(IReadOnlyList<CashFundingRequest> Items, int Total)> SearchAsync(
        Guid tenantId,
        Guid? branchId,
        Guid? requestedByUserId,
        CashFundingRequestStatus? status,
        Guid? cashRegisterId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task AddAsync(CashFundingRequest request, CancellationToken ct = default);
}
