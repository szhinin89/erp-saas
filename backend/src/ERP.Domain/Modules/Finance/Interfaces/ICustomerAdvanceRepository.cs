using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

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

    Task AddAsync(CustomerAdvance advance, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
