using ERP.Domain.Subscriptions;

namespace ERP.Application.Subscriptions;

/// <summary>Lectura conjunta del menú JSON del plan y preferencia de layout.</summary>
public sealed record PlanMenuReadDto(string? MenuConfigJson, string MenuSidebarLayout);

public sealed record CommercialPlanFeatureAdminDto(
    Guid FeatureId,
    string FeatureCode,
    string FeatureName,
    bool IsMetered,
    PlatformFeatureKind Kind,
    string? ResourceRef,
    bool IsIncluded,
    long? LimitPerPeriod);

public sealed record CommercialPlanAdminDto(
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
    bool HasMenuConfig,
    string MenuSidebarLayout,
    IReadOnlyList<CommercialPlanFeatureAdminDto> Features);

public sealed record CreateCommercialPlanRequest(
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
    string? ExternalBillingRef);

public sealed record UpdateCommercialPlanRequest(
    string Name,
    string? ShortLabel,
    bool IsActive,
    decimal PriceAmount,
    string Currency,
    string BillingCycle,
    bool IsPubliclyVisible,
    string? ExternalBillingRef,
    string? MenuSidebarLayout = null);

/// <summary>Copiar configuración de menú desde un plan origen hacia otro.</summary>
public sealed record CopyPlanFromRequest
{
    public bool CopyMenu { get; init; } = true;
}

/// <summary>DTO público para landing / precios (solo planes visibles y activos).</summary>
public sealed record SaasPublicPlanDto(
    Guid Id,
    string Code,
    string Name,
    string? ShortLabel,
    decimal PriceAmount,
    string Currency,
    string BillingCycle,
    bool IsRecommended,
    int SortOrder,
    IReadOnlyList<SaasPublicPlanFeatureDto> Features);

public sealed record SaasPublicPlanFeatureDto(
    string Code,
    string Name,
    string? Description,
    bool IsMetered,
    string Kind,
    string? ResourceRef,
    bool IsIncluded,
    long? LimitPerPeriod);
