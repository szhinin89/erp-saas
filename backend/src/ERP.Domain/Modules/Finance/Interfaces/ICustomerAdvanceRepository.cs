using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Interfaces;

/// <summary>Filtros opcionales del listado de anticipos.</summary>
public sealed record CustomerAdvanceSearchCriteria(
    Guid? CustomerId,
    CustomerAdvanceStatus? Status,
    Guid? BranchId
);

/// <summary>
/// ADR-052 — persistencia de <see cref="CustomerAdvance"/>. Toda lectura devuelve el agregado con su
/// libro completo y está acotada a tenant y empresa operativa (fuera de alcance → null). La sucursal
/// nunca filtra.
/// </summary>
public interface ICustomerAdvanceRepository
{
    Task<CustomerAdvance?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Bloquea la fila (<c>FOR UPDATE</c>) hasta el fin de la transacción ambiente y recarga bajo el
    /// lock: la decisión de saldo se toma solo con esta lectura. Requiere transacción abierta.
    /// </summary>
    Task<CustomerAdvance?> GetByIdForUpdateAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    /// <summary>Anticipo de la empresa que ya registró esta intención del cliente (replay).</summary>
    Task<CustomerAdvance?> GetByClientRequestIdAsync(
        Guid tenantId,
        Guid clientRequestId,
        CancellationToken ct = default
    );

    /// <summary>Página de la empresa, sin movimientos, del anticipo más reciente al más antiguo.</summary>
    Task<(IReadOnlyList<CustomerAdvance> Items, int Total)> SearchAsync(
        Guid tenantId,
        CustomerAdvanceSearchCriteria criteria,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>Σ disponible de los anticipos <c>Confirmed</c> del cliente en la empresa.</summary>
    Task<decimal> GetAvailableBalanceAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Aplicaciones recibidas por una CxC y sus reversas, de la empresa, en orden de registro.
    /// </summary>
    Task<IReadOnlyList<CustomerAdvanceMovement>> ListReceivableMovementsAsync(
        Guid tenantId,
        Guid receivableId,
        CancellationToken ct = default
    );

    Task AddAsync(CustomerAdvance advance, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
