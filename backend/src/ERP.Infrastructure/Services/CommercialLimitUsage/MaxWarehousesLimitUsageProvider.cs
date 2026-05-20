using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services.CommercialLimitUsage;

public sealed class MaxWarehousesLimitUsageProvider : ICommercialLimitUsageProvider
{
    private readonly ErpDbContext _db;

    public MaxWarehousesLimitUsageProvider(ErpDbContext db) => _db = db;

    public bool Supports(string limitCode)
        => string.Equals(limitCode, CommercialPlanLimit.Codes.MaxWarehouses, StringComparison.OrdinalIgnoreCase);

    public async Task<long> GetCurrentUsageAsync(Guid subscriberId, CancellationToken ct = default)
        => await _db.Warehouses.AsNoTracking()
            .CountAsync(w => w.SubscriberId == subscriberId && w.IsActive, ct);
}
