using System.Globalization;
using ERP.Application.Admin;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Admin.UseCases.SuperAdminGlobal;

public sealed record GetInstanceQuotaQuery : IRequest<Result<InstanceQuotaFileModel>>;

public sealed record UpdateInstanceQuotaCommand(InstanceQuotaFileModel Body) : IRequest<Result<bool>>;

public sealed record SuperAdminMetricsDto(
    SuperAdminMetricsTotalsDto Totals,
    IReadOnlyList<SuperAdminRecentSubscriberDto> RecentSubscribers);

public sealed record SuperAdminMetricsTotalsDto(
    int TotalSubscribers,
    int ActiveSubscribers,
    int TotalUsers,
    int ActiveUsers);

public sealed record SuperAdminRecentSubscriberDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTime CreatedAt);

public sealed record GetSuperAdminMetricsQuery : IRequest<Result<SuperAdminMetricsDto>>;

public sealed record GetSuperAdminPlansCatalogQuery : IRequest<Result<object>>;

public sealed record GetSuperAdminGrowthAnalyticsQuery(
    string? From,
    string? To,
    string? Granularity) : IRequest<Result<GrowthAnalyticsResponseDto>>;

public sealed record GetSuperAdminGrowthMonetaryQuery(
    string? From,
    string? To,
    string? Granularity) : IRequest<Result<GrowthMonetaryResponseDto>>;

public sealed record GetSuperAdminNavigationMenuQuery : IRequest<Result<AdminNavigationMenuResponse>>;

public sealed record ReorderSuperAdminNavigationGroupsCommand(IReadOnlyList<Guid> OrderedGroupIds) : IRequest<Result<bool>>;

public sealed record ReorderSuperAdminNavigationItemLevelsCommand(IReadOnlyList<NavItemSiblingOrderDto> Levels)
    : IRequest<Result<bool>>;

public sealed record CreateSuperAdminNavigationMenuItemCommand(CreateNavItemRequest Body) : IRequest<Result<Guid>>;

public sealed record UpdateSuperAdminNavigationMenuItemCommand(Guid ItemId, UpdateNavItemRequest Body) : IRequest<Result<bool>>;

public sealed record DeleteSuperAdminNavigationMenuItemCommand(Guid ItemId) : IRequest<Result<bool>>;

public sealed record RevokeSuperAdminUserSessionsCommand(Guid UserId, Guid SubscriberId) : IRequest<Result<string>>;

public sealed class GetInstanceQuotaHandler : IRequestHandler<GetInstanceQuotaQuery, Result<InstanceQuotaFileModel>>
{
    private readonly IDeploymentFeatureFlags _deployment;

    public GetInstanceQuotaHandler(IDeploymentFeatureFlags deployment) => _deployment = deployment;

    public Task<Result<InstanceQuotaFileModel>> Handle(GetInstanceQuotaQuery request, CancellationToken ct)
    {
        var dto = new InstanceQuotaFileModel
        {
            DedicatedSingleClientInstance = _deployment.IsDedicatedSingleClientInstance,
            MaxActiveSubscribers = _deployment.MaxActiveSubscribers,
            MaxIdentityUsers = _deployment.MaxIdentityUsers,
            MaxUsersPerSubscriber = _deployment.MaxUsersPerSubscriber,
        };
        return Task.FromResult(Result<InstanceQuotaFileModel>.Success(dto));
    }
}

public sealed class UpdateInstanceQuotaHandler : IRequestHandler<UpdateInstanceQuotaCommand, Result<bool>>
{
    private readonly IInstanceQuotaPersistence _persistence;

    public UpdateInstanceQuotaHandler(IInstanceQuotaPersistence persistence) => _persistence = persistence;

    public Task<Result<bool>> Handle(UpdateInstanceQuotaCommand request, CancellationToken ct)
    {
        var body = request.Body;
        if (body.DedicatedSingleClientInstance == true &&
            (!body.MaxActiveSubscribers.HasValue || body.MaxActiveSubscribers <= 0))
        {
            return Task.FromResult(Result<bool>.Failure(
                "En instancia dedicada debe indicar maxActiveTenants (máximo de empresas/RUC) mayor que cero."));
        }

        _persistence.Save(body);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed class GetSuperAdminMetricsHandler : IRequestHandler<GetSuperAdminMetricsQuery, Result<SuperAdminMetricsDto>>
{
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IAccessRepository _accessRepository;

    public GetSuperAdminMetricsHandler(
        ISubscriberRepository subscriberRepository,
        IAccessRepository accessRepository)
    {
        _subscriberRepository = subscriberRepository;
        _accessRepository = accessRepository;
    }

    public async Task<Result<SuperAdminMetricsDto>> Handle(GetSuperAdminMetricsQuery request, CancellationToken ct)
    {
        var subscribers = await _subscriberRepository.GetAllAsync(ct);
        var activeSubscribers = subscribers.Count(t => t.IsActive);
        var totalSubscribers = subscribers.Count;

        var totalUsers = await _accessRepository.CountIdentityUsersAsync(ct);
        var activeUsers = await _accessRepository.CountActiveCompanyUsersAsync(ct)
                          + await _accessRepository.CountActivePlatformUsersAsync(ct);

        var recentSubscribers = subscribers
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Select(t => new SuperAdminRecentSubscriberDto(t.Id, t.Name, t.Slug, t.IsActive, t.CreatedAt))
            .ToList();

        return Result<SuperAdminMetricsDto>.Success(new SuperAdminMetricsDto(
            new SuperAdminMetricsTotalsDto(totalSubscribers, activeSubscribers, totalUsers, activeUsers),
            recentSubscribers));
    }
}

public sealed class GetSuperAdminPlansCatalogHandler : IRequestHandler<GetSuperAdminPlansCatalogQuery, Result<object>>
{
    private readonly ICommercialCatalogQuery _saasCatalogQuery;

    public GetSuperAdminPlansCatalogHandler(ICommercialCatalogQuery saasCatalogQuery) =>
        _saasCatalogQuery = saasCatalogQuery;

    public async Task<Result<object>> Handle(GetSuperAdminPlansCatalogQuery request, CancellationToken ct)
    {
        var plans = await _saasCatalogQuery.GetPlansWithFeaturesAsync(ct);
        return Result<object>.Success(new { plans });
    }
}

public sealed class GetSuperAdminGrowthAnalyticsHandler
    : IRequestHandler<GetSuperAdminGrowthAnalyticsQuery, Result<GrowthAnalyticsResponseDto>>
{
    private readonly IGrowthAnalyticsReader _growthAnalytics;

    public GetSuperAdminGrowthAnalyticsHandler(IGrowthAnalyticsReader growthAnalytics) =>
        _growthAnalytics = growthAnalytics;

    public async Task<Result<GrowthAnalyticsResponseDto>> Handle(
        GetSuperAdminGrowthAnalyticsQuery request,
        CancellationToken ct)
    {
        var (ok, err, fromUtc, toUtc, granularity) =
            SuperAdminGrowthRangeParser.Parse(request.From, request.To, request.Granularity);
        if (!ok)
            return Result<GrowthAnalyticsResponseDto>.Failure(err!);

        var dto = await _growthAnalytics.GetSeriesAsync(fromUtc, toUtc, granularity, ct);
        return Result<GrowthAnalyticsResponseDto>.Success(dto);
    }
}

public sealed class GetSuperAdminGrowthMonetaryHandler
    : IRequestHandler<GetSuperAdminGrowthMonetaryQuery, Result<GrowthMonetaryResponseDto>>
{
    private readonly IGrowthAnalyticsReader _growthAnalytics;

    public GetSuperAdminGrowthMonetaryHandler(IGrowthAnalyticsReader growthAnalytics) =>
        _growthAnalytics = growthAnalytics;

    public async Task<Result<GrowthMonetaryResponseDto>> Handle(
        GetSuperAdminGrowthMonetaryQuery request,
        CancellationToken ct)
    {
        var (ok, err, fromUtc, toUtc, granularity) =
            SuperAdminGrowthRangeParser.Parse(request.From, request.To, request.Granularity);
        if (!ok)
            return Result<GrowthMonetaryResponseDto>.Failure(err!);

        var dto = await _growthAnalytics.GetMonetarySeriesAsync(fromUtc, toUtc, granularity, ct);
        return Result<GrowthMonetaryResponseDto>.Success(dto);
    }
}

public sealed class GetSuperAdminNavigationMenuHandler
    : IRequestHandler<GetSuperAdminNavigationMenuQuery, Result<AdminNavigationMenuResponse>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public GetSuperAdminNavigationMenuHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<AdminNavigationMenuResponse>> Handle(
        GetSuperAdminNavigationMenuQuery request,
        CancellationToken ct)
    {
        var menu = await _navigationMenuAdmin.GetMenuTreeAsync(ct);
        return Result<AdminNavigationMenuResponse>.Success(menu);
    }
}

public sealed class ReorderSuperAdminNavigationGroupsHandler : IRequestHandler<ReorderSuperAdminNavigationGroupsCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public ReorderSuperAdminNavigationGroupsHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(ReorderSuperAdminNavigationGroupsCommand request, CancellationToken ct)
    {
        if (request.OrderedGroupIds is not { Count: > 0 })
            return Result<bool>.Failure("orderedGroupIds requerido.");

        var (ok, err) = await _navigationMenuAdmin.ReorderGroupsAsync(request.OrderedGroupIds, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class ReorderSuperAdminNavigationItemLevelsHandler
    : IRequestHandler<ReorderSuperAdminNavigationItemLevelsCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public ReorderSuperAdminNavigationItemLevelsHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(ReorderSuperAdminNavigationItemLevelsCommand request, CancellationToken ct)
    {
        if (request.Levels is not { Count: > 0 })
            return Result<bool>.Failure("levels requerido.");

        var (ok, err) = await _navigationMenuAdmin.ReorderItemLevelsAsync(request.Levels, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class CreateSuperAdminNavigationMenuItemHandler
    : IRequestHandler<CreateSuperAdminNavigationMenuItemCommand, Result<Guid>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public CreateSuperAdminNavigationMenuItemHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<Guid>> Handle(CreateSuperAdminNavigationMenuItemCommand request, CancellationToken ct)
    {
        var (ok, newId, err) = await _navigationMenuAdmin.CreateNavItemAsync(request.Body, ct);
        if (!ok || newId is null)
            return Result<Guid>.Failure(err ?? "Error");

        return Result<Guid>.Success(newId.Value);
    }
}

public sealed class UpdateSuperAdminNavigationMenuItemHandler
    : IRequestHandler<UpdateSuperAdminNavigationMenuItemCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public UpdateSuperAdminNavigationMenuItemHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(UpdateSuperAdminNavigationMenuItemCommand request, CancellationToken ct)
    {
        var (ok, err) = await _navigationMenuAdmin.UpdateNavItemAsync(request.ItemId, request.Body, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class DeleteSuperAdminNavigationMenuItemHandler
    : IRequestHandler<DeleteSuperAdminNavigationMenuItemCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public DeleteSuperAdminNavigationMenuItemHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(DeleteSuperAdminNavigationMenuItemCommand request, CancellationToken ct)
    {
        var (ok, err) = await _navigationMenuAdmin.DeleteNavItemAsync(request.ItemId, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class RevokeSuperAdminUserSessionsHandler : IRequestHandler<RevokeSuperAdminUserSessionsCommand, Result<string>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IRefreshTokenService _refreshTokenService;

    public RevokeSuperAdminUserSessionsHandler(
        IAccessRepository accessRepository,
        IRefreshTokenService refreshTokenService)
    {
        _accessRepository = accessRepository;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<string>> Handle(RevokeSuperAdminUserSessionsCommand request, CancellationToken ct)
    {
        var user = await _accessRepository.GetUserByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<string>.Failure("Usuario no encontrado.");

        await _refreshTokenService.RevokeAllForUserAsync(
            request.UserId,
            request.SubscriberId,
            "Revocación administrativa",
            ct);

        return Result<string>.Success($"Sesiones del usuario {request.UserId} revocadas.");
    }
}

internal static class SuperAdminGrowthRangeParser
{
    internal static (bool Ok, string? Error, DateTime FromUtc, DateTime ToUtc, string Granularity) Parse(
        string? from,
        string? to,
        string? granularity)
    {
        var toUtc = ParseDateOrDefault(to, DateTime.UtcNow.Date);
        var fromUtc = ParseDateOrDefault(from, toUtc.AddMonths(-3));
        if ((toUtc - fromUtc).TotalDays > 800)
        {
            return (false, "El rango máximo permitido es aproximadamente 24 meses.", default, default, string.Empty);
        }

        var g = (granularity ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(g))
        {
            var spanDays = (toUtc - fromUtc).TotalDays;
            g = spanDays > 120 ? "month" : spanDays > 35 ? "week" : "day";
        }

        return (true, null, fromUtc, toUtc, g);
    }

    private static DateTime ParseDateOrDefault(string? isoDate, DateTime fallbackUtcDate)
    {
        if (!string.IsNullOrWhiteSpace(isoDate) &&
            DateTime.TryParse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        }

        return DateTime.SpecifyKind(fallbackUtcDate.Date, DateTimeKind.Utc);
    }
}
