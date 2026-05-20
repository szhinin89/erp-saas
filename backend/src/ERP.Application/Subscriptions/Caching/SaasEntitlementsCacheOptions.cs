namespace ERP.Application.Subscriptions.Caching;

public sealed class SaasEntitlementsCacheOptions
{
    public const string SectionName = "SaasEntitlementsCache";

    public bool Enabled { get; set; } = true;

    public int TtlSeconds { get; set; } = 300;
}
