using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Caja;

public sealed class CashMovementReasonRepository : ICashMovementReasonRepository
{
    private readonly ErpDbContext _db;

    public CashMovementReasonRepository(ErpDbContext db) => _db = db;

    public async Task AddAsync(CashMovementReason reason, CancellationToken ct = default) =>
        await _db.Set<CashMovementReason>().AddAsync(reason, ct);

    public Task<CashMovementReason?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db.Set<CashMovementReason>()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.CompanyId == companyId && r.Id == id,
                ct
            );

    public Task<CashMovementReason?> GetByCodeAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken ct = default
    )
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Set<CashMovementReason>()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.CompanyId == companyId && r.Code == normalized,
                ct
            );
    }

    public async Task<IReadOnlyList<CashMovementReason>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CashMovementType? movementType,
        bool includeInactive,
        CancellationToken ct = default
    )
    {
        var q = _db
            .Set<CashMovementReason>()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId);

        if (!includeInactive)
            q = q.Where(r => r.IsActive);
        if (movementType.HasValue)
            q = q.Where(r => r.MovementType == movementType.Value);

        return await q.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
