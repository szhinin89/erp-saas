using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

public sealed class BankRepository : IBankRepository
{
    private readonly ErpDbContext _db;

    public BankRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<Bank>> ListAsync(
        Guid tenantId,
        bool onlyActive = false,
        string? search = null,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db.Banks.Where(x => x.TenantId == tenantId);

        if (onlyActive)
            q = q.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Code, pattern)
                || EF.Functions.ILike(x.Name, pattern)
                || (x.ShortName != null && EF.Functions.ILike(x.ShortName, pattern))
            );
        }

        return await q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Bank?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        _db.Banks.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string countryCode,
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default
    )
    {
        var upperCountry = countryCode.Trim().ToUpperInvariant();
        var upperCode = code.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        var q = _db.Banks.Where(x =>
            x.TenantId == tenantId && x.CountryCode == upperCountry && x.Code == upperCode
        );
        if (excludeId.HasValue)
            q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Bank entity, CancellationToken cancellationToken = default) =>
        await _db.Banks.AddAsync(entity, cancellationToken);

    public void Update(Bank entity) => _db.Banks.Update(entity);
}
