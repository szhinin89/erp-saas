using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

public interface ICustomerClassificationRepository
{
    Task<IReadOnlyList<CustomerClassification>> GetActiveAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    );
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken ct = default
    );
    Task<bool> CodeExistsActiveAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken ct = default
    );
    Task AddAsync(CustomerClassification entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
