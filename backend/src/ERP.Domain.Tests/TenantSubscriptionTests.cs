using FluentAssertions;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Domain.Tests;

public sealed class SubscriberSubscriptionTests
{
    [Fact]
    public void Create_trims_plan_code()
    {
        var tenant = Subscriber.Create(
            "Demo",
            "demo",
            Guid.NewGuid(),
            planCode: "  enterprise  ");

        tenant.PlanCode.Should().Be("enterprise");
    }

    [Fact]
    public void Create_without_plan_leaves_plan_null()
    {
        var tenant = Subscriber.Create("X", "x", Guid.NewGuid(), planCode: null);
        tenant.PlanCode.Should().BeNull();
    }

    [Fact]
    public void SetPlanCode_updates_plan()
    {
        var tenant = Subscriber.Create("Y", "y", Guid.NewGuid());
        tenant.SetPlanCode("lite", Guid.NewGuid());
        tenant.PlanCode.Should().Be("lite");
    }
}
