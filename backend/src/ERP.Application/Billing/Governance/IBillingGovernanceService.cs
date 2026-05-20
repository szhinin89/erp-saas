using ERP.Domain.Billing.Enums;

namespace ERP.Application.Billing.Governance;

public sealed record BillingGovernanceState(
    Guid SubscriberId,
    BillingAccountStatus? BillingStatus,
    BillingRenewalState RenewalState,
    BillingTrialState TrialState,
    bool AllowsPlatformAccess,
    DateTime? GracePeriodEndsAtUtc,
    DateTime? CurrentPeriodEndUtc);

public interface IBillingGovernanceService
{
    Task<BillingGovernanceState> GetStateAsync(Guid subscriberId, CancellationToken ct = default);

    Task<bool> CanAccessPlatformAsync(Guid subscriberId, CancellationToken ct = default);

    Task EnsureBillingAccountAsync(
        Guid subscriberId,
        string billingEmail,
        Guid actorId,
        CancellationToken ct = default);

    Task StartGracePeriodAsync(
        Guid subscriberId,
        int graceDays,
        Guid actorId,
        CancellationToken ct = default);

    Task SuspendForNonPaymentAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default);

    Task ReactivateAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default);

    Task RecordEventAsync(
        Guid subscriberId,
        string eventType,
        BillingEventSource source,
        Guid actorId,
        string? payloadJson = null,
        string? correlationId = null,
        CancellationToken ct = default);
}
