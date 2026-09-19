using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Domain.Modules.Caja.Interfaces;

public interface ICashMovementReasonRepository
{
    Task AddAsync(CashMovementReason reason, CancellationToken ct = default);

    /// <summary>
    /// Fail-closed: filtra por Tenant Y Company en la misma consulta — un motivo de otra empresa
    /// (aunque sea del mismo tenant) o de otro tenant se trata exactamente igual que "no existe".
    /// </summary>
    Task<CashMovementReason?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    Task<CashMovementReason?> GetByCodeAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<CashMovementReason>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CashMovementType? movementType,
        bool includeInactive,
        CancellationToken ct = default
    );

    Task SaveChangesAsync(CancellationToken ct = default);
}
