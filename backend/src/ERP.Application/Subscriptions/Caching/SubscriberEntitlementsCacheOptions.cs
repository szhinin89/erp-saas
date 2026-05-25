namespace ERP.Application.Subscriptions.Caching;

public sealed class SubscriberEntitlementsCacheOptions
{
    public const string SectionName = "SubscriberEntitlementsCache";

    public bool Enabled { get; set; } = true;

    public int TtlSeconds { get; set; } = 300;
}
