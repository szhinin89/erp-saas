using ERP.Domain.Modules.Pricing.Entities;

namespace ERP.Domain.Modules.Pricing.Interfaces;

public interface IPriceListRepository
{
    Task<PriceList?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PriceList>> GetAllAsync(Guid tenantId, bool? activeFilter, string? search, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludeId, CancellationToken ct = default);
    Task<bool> DefaultExistsAsync(Guid tenantId, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(PriceList priceList, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
