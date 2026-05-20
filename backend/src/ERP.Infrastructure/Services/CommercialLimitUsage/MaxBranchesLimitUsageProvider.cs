using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services.CommercialLimitUsage;

public sealed class MaxBranchesLimitUsageProvider : ICommercialLimitUsageProvider
{
    private readonly ErpDbContext _db;

    public MaxBranchesLimitUsageProvider(ErpDbContext db) => _db = db;

    public bool Supports(string limitCode)
        => string.Equals(limitCode, CommercialPlanLimit.Codes.MaxBranches, StringComparison.OrdinalIgnoreCase);

    public async Task<long> GetCurrentUsageAsync(Guid subscriberId, CancellationToken ct = default)
        => await _db.Branches.AsNoTracking()
            .CountAsync(b => b.SubscriberId == subscriberId && b.IsActive, ct);
}
