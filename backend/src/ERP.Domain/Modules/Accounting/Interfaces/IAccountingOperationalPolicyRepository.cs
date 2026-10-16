using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — versiones de la política operativa de Accounting por Company y el
/// lock transaccional que serializa: cambios de política y cierre de período (exclusivo) frente a
/// la creación de obligaciones contables (compartido).
/// </summary>
public interface IAccountingOperationalPolicyRepository
{
    /// <summary>Versiones de la Company ordenadas por <c>EffectiveFrom</c> (tracked).</summary>
    Task<IReadOnlyList<AccountingOperationalPolicy>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    );

    Task AddAsync(AccountingOperationalPolicy policy, CancellationToken ct = default);

    /// <summary>Lock compartido de transacción por Company (creación de obligaciones).</summary>
    Task AcquireSharedLockAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>Lock exclusivo de transacción por Company (cambio de política, cierre de período).</summary>
    Task AcquireExclusiveLockAsync(Guid companyId, CancellationToken ct = default);
}
