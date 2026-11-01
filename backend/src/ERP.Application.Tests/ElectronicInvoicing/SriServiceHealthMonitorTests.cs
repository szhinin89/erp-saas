using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.Enums;
using ERP.Application.Modules.ElectronicInvoicing.Health;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ERP.Application.Tests.ElectronicInvoicing;

public sealed class SriServiceHealthMonitorTests
{
    private const string TestingUrl =
        "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";
    private const string ProductionUrl =
        "https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private readonly ManualTimeProvider _clock = new();

    private SriServiceHealthMonitor NewMonitor() =>
        new(Options.Create(new SriHealthOptions()), _clock);

    private static void Call(
        SriServiceHealthMonitor monitor,
        SriObservationOutcome outcome,
        SriServiceKind service = SriServiceKind.Reception,
        string url = TestingUrl,
        double? seconds = 1
    ) =>
        monitor.Record(
            url,
            service,
            outcome,
            seconds is null ? null : TimeSpan.FromSeconds(seconds.Value)
        );

    [Fact]
    public void Without_evidence_the_state_is_unverified()
    {
        var status = NewMonitor().GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Unverified);
        status.Reason.Should().Be(SriServiceHealthReason.NoRecentEvidence);
        status.LastObservedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Recent_valid_responses_are_available_with_last_observation_time()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.Responded);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Available);
        status.Reason.Should().Be(SriServiceHealthReason.RecentResponses);
        status.Evidence.Should().Be(SriHealthEvidence.Call);
        status.LastObservedAtUtc.Should().Be(_clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public void A_single_transport_failure_is_intermittent_not_unavailable()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.Responded);
        Call(monitor, SriObservationOutcome.TransportFailure);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Intermittent);
        status.Reason.Should().Be(SriServiceHealthReason.RecentTransportFailure);
    }

    [Fact]
    public void Two_consecutive_transport_failures_indicate_possible_unavailability()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.Responded);
        Call(monitor, SriObservationOutcome.TransportFailure);
        Call(monitor, SriObservationOutcome.TransportFailure);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Unavailable);
        status.Reason.Should().Be(SriServiceHealthReason.ConsecutiveTransportFailures);
    }

    [Fact]
    public void A_success_after_failures_is_intermittent_and_recovers_when_failures_expire()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.TransportFailure);
        Call(monitor, SriObservationOutcome.TransportFailure);
        _clock.Advance(TimeSpan.FromMinutes(10));
        Call(monitor, SriObservationOutcome.Responded);

        monitor.GetStatus(TestingUrl, SriServiceKind.Reception).Reason
            .Should().Be(SriServiceHealthReason.RecoveredAfterFailures);

        _clock.Advance(TimeSpan.FromMinutes(6));
        monitor.GetStatus(TestingUrl, SriServiceKind.Reception).State
            .Should().Be(SriServiceHealthState.Available);
    }

    [Fact]
    public void Exhausted_polling_with_valid_empty_responses_is_neither_slow_nor_unavailable()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.RespondedWithoutDecision, SriServiceKind.Authorization, seconds: null);
        Call(monitor, SriObservationOutcome.RespondedWithoutDecision, SriServiceKind.Authorization, seconds: null);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Authorization);

        status.State.Should().Be(SriServiceHealthState.Available);
        status.Reason.Should().Be(SriServiceHealthReason.RespondedWithoutDecision);
    }

    [Fact]
    public void Soap_faults_are_intermittent_and_never_unavailable()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.InvalidResponse);
        Call(monitor, SriObservationOutcome.InvalidResponse);
        Call(monitor, SriObservationOutcome.InvalidResponse);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Intermittent);
        status.Reason.Should().Be(SriServiceHealthReason.RecentInvalidResponse);
    }

    [Fact]
    public void Undetermined_failures_are_intermittent_and_never_unavailable()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.Undetermined, SriServiceKind.Authorization, seconds: null);
        Call(monitor, SriObservationOutcome.Undetermined, SriServiceKind.Authorization, seconds: null);

        monitor.GetStatus(TestingUrl, SriServiceKind.Authorization).State
            .Should().Be(SriServiceHealthState.Intermittent);
    }

    [Fact]
    public void A_responded_call_at_or_over_eight_seconds_is_slow()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.Responded, seconds: 8.5);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Slow);
        status.Reason.Should().Be(SriServiceHealthReason.SlowResponse);
    }

    [Fact]
    public void Evidence_older_than_fifteen_minutes_is_unverified()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.TransportFailure);
        Call(monitor, SriObservationOutcome.TransportFailure);
        _clock.Advance(TimeSpan.FromMinutes(16));

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Unverified);
        status.Reason.Should().Be(SriServiceHealthReason.NoRecentEvidence);
    }

    [Fact]
    public void A_restart_starts_unverified()
    {
        var before = NewMonitor();
        Call(before, SriObservationOutcome.Responded);

        NewMonitor().GetStatus(TestingUrl, SriServiceKind.Reception).State
            .Should().Be(SriServiceHealthState.Unverified);
    }

    [Fact]
    public void Testing_and_production_hosts_are_isolated()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.TransportFailure, url: TestingUrl);
        Call(monitor, SriObservationOutcome.TransportFailure, url: TestingUrl);
        Call(monitor, SriObservationOutcome.Responded, url: ProductionUrl);

        monitor.GetStatus(TestingUrl, SriServiceKind.Reception).State
            .Should().Be(SriServiceHealthState.Unavailable);
        monitor.GetStatus(ProductionUrl, SriServiceKind.Reception).State
            .Should().Be(SriServiceHealthState.Available);
    }

    [Fact]
    public void Reception_and_authorization_are_isolated()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.TransportFailure);
        Call(monitor, SriObservationOutcome.TransportFailure);

        monitor.GetStatus(TestingUrl, SriServiceKind.Authorization).State
            .Should().Be(SriServiceHealthState.Unverified);
    }

    [Fact]
    public void Failed_wsdl_pings_alone_never_diagnose_an_outage()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.ProbeUnreachable, seconds: null);
        Call(monitor, SriObservationOutcome.ProbeUnreachable, seconds: null);
        Call(monitor, SriObservationOutcome.ProbeUnreachable, seconds: null);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Intermittent);
        status.Reason.Should().Be(SriServiceHealthReason.ProbeUnreachable);
        status.Evidence.Should().Be(SriHealthEvidence.Probe);
    }

    [Fact]
    public void A_reachable_ping_alone_is_available_marked_as_probe_evidence()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.ProbeReachable, seconds: null);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Available);
        status.Reason.Should().Be(SriServiceHealthReason.ProbeReachable);
        status.Evidence.Should().Be(SriHealthEvidence.Probe);
    }

    [Fact]
    public void A_reachable_ping_after_consecutive_failures_does_not_assert_the_outage()
    {
        var monitor = NewMonitor();
        Call(monitor, SriObservationOutcome.TransportFailure);
        Call(monitor, SriObservationOutcome.TransportFailure);
        Call(monitor, SriObservationOutcome.ProbeReachable, seconds: null);

        var status = monitor.GetStatus(TestingUrl, SriServiceKind.Reception);

        status.State.Should().Be(SriServiceHealthState.Intermittent);
        status.Reason.Should().Be(SriServiceHealthReason.ProbeReachableAfterFailures);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-una-url")]
    public void Invalid_urls_are_ignored_and_read_as_not_configured(string? url)
    {
        var monitor = NewMonitor();
        monitor.Record(url, SriServiceKind.Reception, SriObservationOutcome.Responded, TimeSpan.FromSeconds(1));

        monitor.GetStatus(url, SriServiceKind.Reception).Reason
            .Should().Be(SriServiceHealthReason.NotConfigured);
    }
}
