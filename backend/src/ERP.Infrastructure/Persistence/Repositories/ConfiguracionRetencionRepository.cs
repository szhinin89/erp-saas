using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class RetentionSettingsRepository : IRetentionSettingsRepository
{
    private readonly ErpDbContext _context;

    public RetentionSettingsRepository(ErpDbContext context) => _context = context;

    public async Task<IReadOnlyList<RetentionSettings>> GetActiveForSupplierAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => await _context.RetentionSettings
            .Where(r => r.TenantId == tenantId && r.IsActive &&
                        (r.SubjectType == "Supplier" || r.SubjectType == "AMBOS"))
            .OrderBy(r => r.TaxType)
            .ThenBy(r => r.SriCode)
            .ToListAsync(ct);

    public Task AddAsync(RetentionSettings entity, CancellationToken ct = default)
        => _context.RetentionSettings.AddAsync(entity, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

