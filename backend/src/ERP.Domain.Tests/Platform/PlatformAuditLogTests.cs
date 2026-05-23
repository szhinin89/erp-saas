using FluentAssertions;
using ERP.Domain.Platform.Audit.Entities;

namespace ERP.Domain.Tests.Platform;

public sealed class PlatformAuditLogTests
{
    [Fact]
    public void Create_stores_action_and_actor()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var log = PlatformAuditLog.Create(
            action: "subscriber.suspended",
            actorUserId: actorId,
            actorEmail: "admin@platform.io",
            targetSubscriberId: targetId,
            notes: "Non-payment");

        log.Id.Should().NotBeEmpty();
        log.Action.Should().Be("subscriber.suspended");
        log.ActorUserId.Should().Be(actorId);
        log.ActorEmail.Should().Be("admin@platform.io");
        log.TargetSubscriberId.Should().Be(targetId);
        log.Notes.Should().Be("Non-payment");
        log.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_lowercases_action()
    {
        var log = PlatformAuditLog.Create("Subscriber.Suspended", null, null, null);
        log.Action.Should().Be("subscriber.suspended");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_throws_for_empty_action(string action)
    {
        var act = () => PlatformAuditLog.Create(action, null, null, null);
        act.Should().Throw<ArgumentException>().WithMessage("*Action*");
    }

    [Fact]
    public void Create_maps_empty_actor_guid_to_null()
    {
        var log = PlatformAuditLog.Create("x", Guid.Empty, null, null);
        log.ActorUserId.Should().BeNull();
    }

    [Fact]
    public void Create_maps_empty_target_guid_to_null()
    {
        var log = PlatformAuditLog.Create("x", null, null, Guid.Empty);
        log.TargetSubscriberId.Should().BeNull();
    }

    [Fact]
    public void Create_truncates_ip_to_max_len()
    {
        var longIp = new string('1', PlatformAuditLog.IpAddressMaxLen + 10);
        var log = PlatformAuditLog.Create("x", null, null, null, ipAddress: longIp);
        log.IpAddress.Should().HaveLength(PlatformAuditLog.IpAddressMaxLen);
    }

    [Fact]
    public void Actions_constants_are_stable()
    {
        PlatformAuditLog.Actions.SubscriberActivated.Should().Be("subscriber.activated");
        PlatformAuditLog.Actions.SubscriberSuspended.Should().Be("subscriber.suspended");
        PlatformAuditLog.Actions.PlanChanged.Should().Be("subscriber.plan_changed");
        PlatformAuditLog.Actions.LoginFailure.Should().Be("platform.login_failure");
    }
}
