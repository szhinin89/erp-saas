using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

public sealed class PaymentProviderCustomer : AuditableEntity
{
    public const int ExternalIdMaxLen = 200;
    public const int MetadataMaxLen = 8000;

    public PaymentProviderType ProviderType { get; private set; }
    public string ExternalCustomerId { get; private set; } = null!;
    public string? ExternalMetadataJson { get; private set; }
    public DateTime SyncedAtUtc { get; private set; }

    private PaymentProviderCustomer() { }

    public static PaymentProviderCustomer Create(
        Guid subscriberId,
        PaymentProviderType providerType,
        string externalCustomerId,
        Guid createdBy,
        string? metadataJson = null)
    {
        var row = new PaymentProviderCustomer
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            ProviderType = providerType,
            ExternalCustomerId = externalCustomerId.Trim(),
            ExternalMetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim(),
            SyncedAtUtc = DateTime.UtcNow,
        };
        row.SetCreated(createdBy);
        return row;
    }

    public void TouchSync(Guid updatedBy)
    {
        SyncedAtUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }
}
