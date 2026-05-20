using ERP.Domain.Modules.Logistics.Entities;

namespace ERP.Domain.Modules.Logistics.Interfaces;

public interface ICarrierRepository
{
    Task<List<Carrier>> GetAllAsync(Guid subscriberId, string? search, bool? isActive, CancellationToken ct = default);
    Task<Carrier?>      GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool>          ExistsIdentificationAsync(Guid subscriberId, string identificationNumber, Guid? excludeId, CancellationToken ct = default);
    Task                AddAsync(Carrier carrier, CancellationToken ct = default);
    Task                SaveChangesAsync(CancellationToken ct = default);
}
