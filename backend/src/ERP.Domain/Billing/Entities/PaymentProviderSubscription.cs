using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

public sealed class PaymentProviderSubscription : AuditableEntity
{
    public const int ExternalIdMaxLen = 200;
    public const int ExternalStatusMaxLen = 64;

    public PaymentProviderType ProviderType { get; private set; }
    public string ExternalSubscriptionId { get; private set; } = null!;
    public Guid? CommercialPlanId { get; private set; }
    public string? ExternalStatus { get; private set; }
    public DateTime? CurrentPeriodStartUtc { get; private set; }
    public DateTime? CurrentPeriodEndUtc { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTime SyncedAtUtc { get; private set; }

    private PaymentProviderSubscription() { }

    public static PaymentProviderSubscription Create(
        Guid subscriberId,
        PaymentProviderType providerType,
        string externalSubscriptionId,
        Guid createdBy,
        Guid? commercialPlanId = null,
        string? externalStatus = null)
    {
        var row = new PaymentProviderSubscription
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            ProviderType = providerType,
            ExternalSubscriptionId = externalSubscriptionId.Trim(),
            CommercialPlanId = commercialPlanId,
            ExternalStatus = string.IsNullOrWhiteSpace(externalStatus) ? null : externalStatus.Trim(),
            SyncedAtUtc = DateTime.UtcNow,
        };
        row.SetCreated(createdBy);
        return row;
    }

    public void SyncPeriod(DateTime? startUtc, DateTime? endUtc, bool cancelAtPeriodEnd, string? externalStatus, Guid updatedBy)
    {
        CurrentPeriodStartUtc = startUtc;
        CurrentPeriodEndUtc = endUtc;
        CancelAtPeriodEnd = cancelAtPeriodEnd;
        ExternalStatus = string.IsNullOrWhiteSpace(externalStatus) ? ExternalStatus : externalStatus.Trim();
        SyncedAtUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }
}
