using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

public interface IPaymentTermRepository
{
    Task<IReadOnlyList<PaymentTerm>> ListAsync(
        Guid tenantId,
        string? search = null,
        CancellationToken cancellationToken = default
    );
    Task<PaymentTerm?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(PaymentTerm entity, CancellationToken cancellationToken = default);
    void Update(PaymentTerm entity);
}
