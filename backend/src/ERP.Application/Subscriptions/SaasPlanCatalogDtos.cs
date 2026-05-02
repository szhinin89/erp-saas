namespace ERP.Application.Subscriptions;

/// <summary>Vista de solo lectura del catálogo de planes para panel SuperAdmin.</summary>
public sealed record SaasPlanFeatureCatalogItem(
    string FeatureCode,
    string FeatureName,
    string? Description,
    bool IsMetered,
    bool IsIncluded,
    long? LimitPerPeriod);

public sealed record SaasPlanCatalogItem(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    IReadOnlyList<SaasPlanFeatureCatalogItem> Features);
