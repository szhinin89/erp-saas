namespace ERP.Application.Subscriptions;

/// <summary>Vista de solo lectura del catálogo de planes (panel platform y consistencia interna).</summary>
public sealed record CommercialPlanFeatureCatalogItem(
    string FeatureCode,
    string FeatureName,
    string? Description,
    bool IsMetered,
    string Kind,
    string? ResourceRef,
    bool IsIncluded,
    long? LimitPerPeriod);

public sealed record CommercialPlanCatalogItem(
    Guid Id,
    string Code,
    string Name,
    string? ShortLabel,
    bool IsActive,
    decimal PriceAmount,
    string Currency,
    string BillingCycle,
    bool IsPubliclyVisible,
    bool IsRecommended,
    int SortOrder,
    string? ExternalBillingRef,
    IReadOnlyList<CommercialPlanFeatureCatalogItem> Features);
