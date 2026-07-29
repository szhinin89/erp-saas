using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

public interface IPaymentMethodRepository
{
    Task<IReadOnlyList<PaymentMethod>> ListAsync(
        Guid tenantId,
        bool onlyActive = true,
        CancellationToken ct = default
    );
    Task<PaymentMethod?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<PaymentMethod?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string code,
        Guid? excludeId = null,
        CancellationToken ct = default
    );
    Task AddAsync(PaymentMethod entity, CancellationToken ct = default);
    void Update(PaymentMethod entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
