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

    Task AddAsync(CashFundingRequest request, CancellationToken ct = default);
}
