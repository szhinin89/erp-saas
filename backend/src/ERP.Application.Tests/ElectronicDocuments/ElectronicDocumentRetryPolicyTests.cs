using ERP.Application.Modules.ElectronicDocuments.Services;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentRetryPolicyTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Eligible_when_no_previous_attempt()
    {
        ElectronicDocumentRetryPolicy.IsEligibleForAutomaticRetry(0, null, Now).Should().BeTrue();
    }

    [Fact]
    public void Not_eligible_when_max_attempts_reached()
    {
        ElectronicDocumentRetryPolicy
            .IsEligibleForAutomaticRetry(
                ElectronicDocumentRetryPolicy.MaxAttempts,
                Now.AddMinutes(-30),
                Now
            )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Not_eligible_within_backoff_window()
    {
        // 1er reintento (retryCount=0 tras el intento inicial que lo dejó en 1... aquí se evalúa
        // el siguiente intento con retryCount=1, backoff de 2 minutos): a los 30s no es elegible.
        ElectronicDocumentRetryPolicy
            .IsEligibleForAutomaticRetry(1, Now.AddSeconds(-30), Now)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Eligible_once_backoff_window_elapsed()
    {
        ElectronicDocumentRetryPolicy
            .IsEligibleForAutomaticRetry(1, Now.AddMinutes(-3), Now)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Eligible_exactly_at_backoff_boundary()
    {
        ElectronicDocumentRetryPolicy
            .IsEligibleForAutomaticRetry(0, Now.AddMinutes(-1), Now)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Uses_last_backoff_step_for_retry_counts_beyond_schedule_length()
    {
        // retryCount=4 (5º intento) usa el último paso del backoff (16 min).
        ElectronicDocumentRetryPolicy
            .IsEligibleForAutomaticRetry(4, Now.AddMinutes(-10), Now)
            .Should()
            .BeFalse();
        ElectronicDocumentRetryPolicy
            .IsEligibleForAutomaticRetry(4, Now.AddMinutes(-17), Now)
            .Should()
            .BeTrue();
    }
}
