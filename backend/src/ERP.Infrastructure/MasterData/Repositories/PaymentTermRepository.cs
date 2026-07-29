using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class PaymentTermRepository : IPaymentTermRepository
{
    private readonly ErpDbContext _db;

    public PaymentTermRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaymentTerm>> ListAsync(
        Guid tenantId,
        string? search = null,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db.PaymentTerms.Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Code, pattern) || EF.Functions.ILike(x.Name, pattern)
            );
        }

        return await q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<PaymentTerm?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        _db.PaymentTerms.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == id,
            cancellationToken
        );

    public async Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default
    )
    {
        var upper = code.Trim().ToUpperInvariant();
        var q = _db.PaymentTerms.Where(x => x.TenantId == tenantId && x.Code == upper);
        if (excludeId.HasValue)
            q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(PaymentTerm entity, CancellationToken cancellationToken = default) =>
        await _db.PaymentTerms.AddAsync(entity, cancellationToken);

    public void Update(PaymentTerm entity) => _db.PaymentTerms.Update(entity);
}
