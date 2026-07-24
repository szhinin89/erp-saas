using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

public interface ICreditTermRepository
{
    Task<CreditTerm?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CreditTerm>> GetAllAsync(Guid tenantId, bool? activeFilter, string? search, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(CreditTerm term, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
