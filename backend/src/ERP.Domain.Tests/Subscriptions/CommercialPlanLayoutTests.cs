using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;

namespace ERP.Domain.Tests.Subscriptions;

public sealed class CommercialPlanLayoutTests
{
    [Fact]
    public void SetMenuSidebarLayout_accepts_horizontal_and_vertical()
    {
        var p = CommercialPlan.Create(
            "test-plan-layout",
            "Test",
            null,
            true,
            0,
            "USD",
            CommercialBillingCycle.Monthly,
            true,
            false,
            0,
            null);

        p.SetMenuSidebarLayout("vertical");
        Assert.Equal(CommercialPlan.MenuSidebarLayoutVertical, p.MenuSidebarLayout);

        p.SetMenuSidebarLayout("HORIZONTAL");
        Assert.Equal(CommercialPlan.MenuSidebarLayoutHorizontal, p.MenuSidebarLayout);
    }

    [Fact]
    public void SetMenuSidebarLayout_rejects_invalid()
    {
        var p = CommercialPlan.Create(
            "test-plan-layout-2",
            "Test",
            null,
            true,
            0,
            "USD",
            CommercialBillingCycle.Monthly,
            true,
            false,
            0,
            null);

        Assert.Throws<ArgumentException>(() => p.SetMenuSidebarLayout("diagonal"));
    }
}
