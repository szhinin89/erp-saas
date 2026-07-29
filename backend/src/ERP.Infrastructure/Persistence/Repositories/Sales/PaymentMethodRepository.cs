using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

public sealed class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly ErpDbContext _db;
    public PaymentMethodRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaymentMethod>> ListAsync(
        Guid tenantId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _db.PaymentMethods
            .Where(x => x.TenantId == tenantId);

        if (onlyActive)
            q = q.Where(x => x.IsActive);

        return await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .AsNoTracking().ToListAsync(ct);
    }

    public async Task<PaymentMethod?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.PaymentMethods
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public async Task<PaymentMethod?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default)
        => await _db.PaymentMethods
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code.ToUpperInvariant(), ct);

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var q = _db.PaymentMethods
            .Where(x => x.TenantId == tenantId && x.Code == code.ToUpperInvariant());
        if (excludeId.HasValue)
            q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task AddAsync(PaymentMethod entity, CancellationToken ct = default)
        => await _db.PaymentMethods.AddAsync(entity, ct);

    public void Update(PaymentMethod entity)
        => _db.PaymentMethods.Update(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
