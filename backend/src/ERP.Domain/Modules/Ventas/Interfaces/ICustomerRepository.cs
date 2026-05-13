using ERP.Domain.Modules.Ventas.Entities;

namespace ERP.Domain.Modules.Ventas.Interfaces;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct = default);

    Task<Customer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<bool> ExistsIdentificationAsync(
        Guid tenantId,
        string identificationType,
        string identificationNumber,
        Guid? excludeCustomerId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Customer>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
