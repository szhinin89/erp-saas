using ERP.Application.Billing.Governance;
using ERP.Application.Subscriptions;
using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Billing.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Moq;

namespace ERP.Infrastructure.Tests.Support;

internal static class EntitlementsTestFactory
{
    public static SubscriberEntitlementsService Create(ErpDbContext ctx, PlatformQueryAccessor platform, CommercialPlanLimitService planLimits)
    {
        var billingGov = new Mock<IBillingGovernanceService>();
        billingGov.Setup(g => g.GetStateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new BillingGovernanceState(
                id, null, BillingRenewalState.Active, BillingTrialState.None, true, null, null));

        return new SubscriberEntitlementsService(
            ctx,
            platform,
            planLimits,
            billingGov.Object,
            Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsCacheOptions { Enabled = false }));
    }
}
