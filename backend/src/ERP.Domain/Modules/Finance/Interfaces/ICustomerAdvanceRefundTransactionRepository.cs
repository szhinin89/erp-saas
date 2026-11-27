using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

/// <summary>
/// ADR-052 — hechos financieros append-only de devoluciones de anticipos, acotados a tenant y
/// empresa operativa. Su concurrencia se serializa con el lock del anticipo.
/// </summary>
public interface ICustomerAdvanceRefundTransactionRepository
{
    Task<CustomerAdvanceRefundTransaction?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    Task<CustomerAdvanceRefundTransaction?> GetByMovementIdAsync(
        Guid tenantId,
        Guid movementId,
        CancellationToken ct = default
    );

    /// <summary>Devoluciones y reversas de un anticipo de la empresa.</summary>
    Task<IReadOnlyList<CustomerAdvanceRefundTransaction>> ListByAdvanceIdAsync(
        Guid tenantId,
        Guid advanceId,
        CancellationToken ct = default
    );

    Task AddAsync(CustomerAdvanceRefundTransaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
