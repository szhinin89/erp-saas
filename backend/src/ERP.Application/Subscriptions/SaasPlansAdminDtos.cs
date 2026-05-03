using ERP.Domain.Subscriptions;

namespace ERP.Application.Subscriptions;

public sealed record SaasFeatureDefinitionAdminDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsMetered,
    SaasFeatureKind Kind,
    string? ResourceRef);

public sealed record SaasPlanFeatureAdminDto(
    Guid FeatureId,
    string FeatureCode,
    string FeatureName,
    bool IsMetered,
    SaasFeatureKind Kind,
    string? ResourceRef,
    bool IsIncluded,
    long? LimitPerPeriod);

public sealed record SaasPlanAdminDto(
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
    IReadOnlyList<SaasPlanFeatureAdminDto> Features);

public sealed record CreateSaasPlanRequest(
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

public sealed record UpdateSaasPlanRequest(
    string Name,
    string? ShortLabel,
    bool IsActive,
    decimal PriceAmount,
    string Currency,
    string BillingCycle,
    bool IsPubliclyVisible,
    string? ExternalBillingRef);

public sealed record PlanFeatureAssignDto(Guid FeatureId, bool IsIncluded, long? LimitPerPeriod);

public sealed record CreateSaasFeatureDefinitionRequest(
    string Code,
    string Name,
    string? Description,
    bool IsMetered,
    SaasFeatureKind Kind,
    string? ResourceRef);

public sealed record UpdateSaasFeatureDefinitionRequest(
    string Name,
    string? Description,
    bool IsMetered,
    SaasFeatureKind Kind,
    string? ResourceRef);

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
