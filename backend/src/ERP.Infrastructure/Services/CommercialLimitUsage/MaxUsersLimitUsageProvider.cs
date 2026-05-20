using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscriptions.Entities;

namespace ERP.Infrastructure.Services.CommercialLimitUsage;

public sealed class MaxUsersLimitUsageProvider : ICommercialLimitUsageProvider
{
    private readonly IAccessRepository _access;

    public MaxUsersLimitUsageProvider(IAccessRepository access) => _access = access;

    public bool Supports(string limitCode)
        => string.Equals(limitCode, CommercialPlanLimit.Codes.MaxUsers, StringComparison.OrdinalIgnoreCase);

    public async Task<long> GetCurrentUsageAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var count = await _access.CountActiveCompanyUserMembershipsBySubscriberAsync(subscriberId, ct);
        return count;
    }
}
