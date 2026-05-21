namespace ERP.Application.Admin;

public sealed record GrowthAnalyticsBucketDto(
    string PeriodStart,
    string PeriodEnd,
    string PeriodLabel,
    int NewSubscribers,
    int NewIdentityUsers,
    int NewCompanyUserMemberships,
    int CumulativeSubscribers,
    int CumulativeIdentityUsers,
    int CumulativeCompanyUserMemberships);

public sealed record GrowthAnalyticsResponseDto(
    string From,
    string To,
    string Granularity,
    IReadOnlyList<GrowthAnalyticsBucketDto> Series);

public sealed record GrowthMonetaryBucketDto(
    string PeriodStart,
    string PeriodEnd,
    string PeriodLabel,
    decimal NewMrrApprox,
    decimal CumulativeMrrApprox);

/// <summary>
/// MRR aproximado por periodo (plan actual del tenant; solo subscribers activos con plan de catálogo activo).
/// CurrencyHint: moneda predominante entre subscribers con MRR &gt; 0, o MIXED si hay varias.
/// </summary>
public sealed record GrowthMonetaryResponseDto(
    string From,
    string To,
    string Granularity,
    string CurrencyHint,
    IReadOnlyList<GrowthMonetaryBucketDto> Series);
