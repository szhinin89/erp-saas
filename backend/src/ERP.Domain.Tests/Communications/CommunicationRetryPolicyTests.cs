using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Services;
using FluentAssertions;

namespace ERP.Domain.Tests.Communications;

/// <summary>ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — política única de reintento y backoff.</summary>
public sealed class CommunicationRetryPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(CommunicationFailureCategory.Transient)]
    [InlineData(CommunicationFailureCategory.Unknown)]
    public void Transient_y_Unknown_reprograman_con_backoff_hasta_agotar_MaxRetries(CommunicationFailureCategory category)
    {
        var first = CommunicationRetryPolicy.OnFailure(0, 3, category, Now);
        first.Status.Should().Be(CommunicationStatus.Pending);
        first.RetryCount.Should().Be(1);
        first.NextAttemptAtUtc.Should().Be(Now.AddMinutes(2));

        var second = CommunicationRetryPolicy.OnFailure(1, 3, category, Now);
        second.Status.Should().Be(CommunicationStatus.Pending);
        second.NextAttemptAtUtc.Should().Be(Now.AddMinutes(4));

        var last = CommunicationRetryPolicy.OnFailure(2, 3, category, Now);
        last.Status.Should().Be(CommunicationStatus.Failed);
        last.RetryCount.Should().Be(3);
        last.NextAttemptAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(CommunicationFailureCategory.Permanent)]
    [InlineData(CommunicationFailureCategory.Configuration)]
    public void Permanent_y_Configuration_son_terminales_al_primer_fallo(CommunicationFailureCategory category)
    {
        var outcome = CommunicationRetryPolicy.OnFailure(0, 20, category, Now);

        outcome.Status.Should().Be(CommunicationStatus.Failed);
        outcome.RetryCount.Should().Be(1);
        outcome.NextAttemptAtUtc.Should().BeNull();
    }

    [Fact]
    public void MaxRetries_cero_falla_al_primer_intento()
    {
        CommunicationRetryPolicy.OnFailure(0, 0, CommunicationFailureCategory.Transient, Now)
            .Status.Should().Be(CommunicationStatus.Failed);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    [InlineData(6, 60)]
    [InlineData(20, 60)]
    [InlineData(int.MaxValue, 60)]
    public void Backoff_exponencial_con_tope_de_60_minutos_sin_overflow(int failedAttempts, int expectedMinutes)
    {
        CommunicationRetryPolicy.Backoff(failedAttempts).Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void Recuperacion_tras_lease_agotada_cuando_RetryCount_alcanza_MaxRetries()
    {
        CommunicationRetryPolicy.IsExhausted(3, 3).Should().BeTrue();
        CommunicationRetryPolicy.IsExhausted(2, 3).Should().BeFalse();
    }
}
