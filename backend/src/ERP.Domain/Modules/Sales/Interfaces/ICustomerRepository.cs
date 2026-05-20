using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct = default);

    Task<Customer?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);

    Task<bool> ExistsIdentificationAsync(
        Guid subscriberId,
        string identificationType,
        string identificationNumber,
        Guid? excludeCustomerId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Customer>> GetAsync(
        Guid subscriberId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
