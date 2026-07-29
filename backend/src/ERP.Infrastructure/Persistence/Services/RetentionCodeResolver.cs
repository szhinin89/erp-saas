using ERP.Application.Modules.Purchases.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Services;

public sealed class RetentionCodeResolver : IRetentionCodeResolver
{
    private readonly ErpDbContext _db;
    public RetentionCodeResolver(ErpDbContext db) => _db = db;

    public async Task<RetentionCodeInfo?> GetRetentionCodeAsync(
        string code, string taxType, CancellationToken ct = default)
    {
        var r = await _db.SriRetentionCodes.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Code == code && x.TaxType == taxType.ToUpperInvariant() && x.IsActive, ct);

        return r is null ? null : new RetentionCodeInfo(r.Code, r.Name, r.Percentage);
    }
}
