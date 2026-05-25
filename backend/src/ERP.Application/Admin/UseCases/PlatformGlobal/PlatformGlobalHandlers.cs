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

namespace ERP.Application.Admin.UseCases.PlatformGlobal;

public sealed record GetInstanceQuotaQuery : IRequest<Result<InstanceQuotaFileModel>>;

public sealed record UpdateInstanceQuotaCommand(InstanceQuotaFileModel Body) : IRequest<Result<bool>>;

public sealed record PlatformMetricsDto(
    PlatformMetricsTotalsDto Totals,
    IReadOnlyList<PlatformRecentSubscriberDto> RecentSubscribers);

public sealed record PlatformMetricsTotalsDto(
    int TotalSubscribers,
    int ActiveSubscribers,
    int TotalUsers,
    int ActiveUsers);

public sealed record PlatformRecentSubscriberDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTime CreatedAt);

public sealed record GetPlatformMetricsQuery : IRequest<Result<PlatformMetricsDto>>;

public sealed record GetPlatformPlansCatalogQuery : IRequest<Result<object>>;

public sealed record GetPlatformGrowthAnalyticsQuery(
    string? From,
    string? To,
    string? Granularity) : IRequest<Result<GrowthAnalyticsResponseDto>>;

public sealed record GetPlatformGrowthMonetaryQuery(
    string? From,
    string? To,
    string? Granularity) : IRequest<Result<GrowthMonetaryResponseDto>>;

public sealed record GetPlatformNavigationMenuQuery : IRequest<Result<AdminNavigationMenuResponse>>;

public sealed record ReorderPlatformNavigationGroupsCommand(IReadOnlyList<Guid> OrderedGroupIds) : IRequest<Result<bool>>;

public sealed record ReorderPlatformNavigationItemLevelsCommand(IReadOnlyList<NavItemSiblingOrderDto> Levels)
    : IRequest<Result<bool>>;

public sealed record CreatePlatformNavigationMenuItemCommand(CreateNavItemRequest Body) : IRequest<Result<Guid>>;

public sealed record UpdatePlatformNavigationMenuItemCommand(Guid ItemId, UpdateNavItemRequest Body) : IRequest<Result<bool>>;

public sealed record DeletePlatformNavigationMenuItemCommand(Guid ItemId) : IRequest<Result<bool>>;

public sealed record RevokePlatformUserSessionsCommand(Guid UserId, Guid SubscriberId) : IRequest<Result<string>>;

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
                "En instancia dedicada debe indicar maxActiveSubscribers (máximo de empresas/RUC) mayor que cero."));
        }

        _persistence.Save(body);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

public sealed class GetPlatformMetricsHandler : IRequestHandler<GetPlatformMetricsQuery, Result<PlatformMetricsDto>>
{
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IAccessRepository _accessRepository;

    public GetPlatformMetricsHandler(
        ISubscriberRepository subscriberRepository,
        IAccessRepository accessRepository)
    {
        _subscriberRepository = subscriberRepository;
        _accessRepository = accessRepository;
    }

    public async Task<Result<PlatformMetricsDto>> Handle(GetPlatformMetricsQuery request, CancellationToken ct)
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
            .Select(t => new PlatformRecentSubscriberDto(t.Id, t.Name, t.Slug, t.IsActive, t.CreatedAt))
            .ToList();

        return Result<PlatformMetricsDto>.Success(new PlatformMetricsDto(
            new PlatformMetricsTotalsDto(totalSubscribers, activeSubscribers, totalUsers, activeUsers),
            recentSubscribers));
    }
}

public sealed class GetPlatformPlansCatalogHandler : IRequestHandler<GetPlatformPlansCatalogQuery, Result<object>>
{
    private readonly ICommercialCatalogQuery _saasCatalogQuery;

    public GetPlatformPlansCatalogHandler(ICommercialCatalogQuery saasCatalogQuery) =>
        _saasCatalogQuery = saasCatalogQuery;

    public async Task<Result<object>> Handle(GetPlatformPlansCatalogQuery request, CancellationToken ct)
    {
        var plans = await _saasCatalogQuery.GetPlansWithFeaturesAsync(ct);
        return Result<object>.Success(new { plans });
    }
}

public sealed class GetPlatformGrowthAnalyticsHandler
    : IRequestHandler<GetPlatformGrowthAnalyticsQuery, Result<GrowthAnalyticsResponseDto>>
{
    private readonly IGrowthAnalyticsReader _growthAnalytics;

    public GetPlatformGrowthAnalyticsHandler(IGrowthAnalyticsReader growthAnalytics) =>
        _growthAnalytics = growthAnalytics;

    public async Task<Result<GrowthAnalyticsResponseDto>> Handle(
        GetPlatformGrowthAnalyticsQuery request,
        CancellationToken ct)
    {
        var (ok, err, fromUtc, toUtc, granularity) =
            PlatformGrowthRangeParser.Parse(request.From, request.To, request.Granularity);
        if (!ok)
            return Result<GrowthAnalyticsResponseDto>.Failure(err!);

        var dto = await _growthAnalytics.GetSeriesAsync(fromUtc, toUtc, granularity, ct);
        return Result<GrowthAnalyticsResponseDto>.Success(dto);
    }
}

public sealed class GetPlatformGrowthMonetaryHandler
    : IRequestHandler<GetPlatformGrowthMonetaryQuery, Result<GrowthMonetaryResponseDto>>
{
    private readonly IGrowthAnalyticsReader _growthAnalytics;

    public GetPlatformGrowthMonetaryHandler(IGrowthAnalyticsReader growthAnalytics) =>
        _growthAnalytics = growthAnalytics;

    public async Task<Result<GrowthMonetaryResponseDto>> Handle(
        GetPlatformGrowthMonetaryQuery request,
        CancellationToken ct)
    {
        var (ok, err, fromUtc, toUtc, granularity) =
            PlatformGrowthRangeParser.Parse(request.From, request.To, request.Granularity);
        if (!ok)
            return Result<GrowthMonetaryResponseDto>.Failure(err!);

        var dto = await _growthAnalytics.GetMonetarySeriesAsync(fromUtc, toUtc, granularity, ct);
        return Result<GrowthMonetaryResponseDto>.Success(dto);
    }
}

public sealed class GetPlatformNavigationMenuHandler
    : IRequestHandler<GetPlatformNavigationMenuQuery, Result<AdminNavigationMenuResponse>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public GetPlatformNavigationMenuHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<AdminNavigationMenuResponse>> Handle(
        GetPlatformNavigationMenuQuery request,
        CancellationToken ct)
    {
        var menu = await _navigationMenuAdmin.GetMenuTreeAsync(ct);
        return Result<AdminNavigationMenuResponse>.Success(menu);
    }
}

public sealed class ReorderPlatformNavigationGroupsHandler : IRequestHandler<ReorderPlatformNavigationGroupsCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public ReorderPlatformNavigationGroupsHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(ReorderPlatformNavigationGroupsCommand request, CancellationToken ct)
    {
        if (request.OrderedGroupIds is not { Count: > 0 })
            return Result<bool>.Failure("orderedGroupIds requerido.");

        var (ok, err) = await _navigationMenuAdmin.ReorderGroupsAsync(request.OrderedGroupIds, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class ReorderPlatformNavigationItemLevelsHandler
    : IRequestHandler<ReorderPlatformNavigationItemLevelsCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public ReorderPlatformNavigationItemLevelsHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(ReorderPlatformNavigationItemLevelsCommand request, CancellationToken ct)
    {
        if (request.Levels is not { Count: > 0 })
            return Result<bool>.Failure("levels requerido.");

        var (ok, err) = await _navigationMenuAdmin.ReorderItemLevelsAsync(request.Levels, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class CreatePlatformNavigationMenuItemHandler
    : IRequestHandler<CreatePlatformNavigationMenuItemCommand, Result<Guid>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public CreatePlatformNavigationMenuItemHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<Guid>> Handle(CreatePlatformNavigationMenuItemCommand request, CancellationToken ct)
    {
        var (ok, newId, err) = await _navigationMenuAdmin.CreateNavItemAsync(request.Body, ct);
        if (!ok || newId is null)
            return Result<Guid>.Failure(err ?? "Error");

        return Result<Guid>.Success(newId.Value);
    }
}

public sealed class UpdatePlatformNavigationMenuItemHandler
    : IRequestHandler<UpdatePlatformNavigationMenuItemCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public UpdatePlatformNavigationMenuItemHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(UpdatePlatformNavigationMenuItemCommand request, CancellationToken ct)
    {
        var (ok, err) = await _navigationMenuAdmin.UpdateNavItemAsync(request.ItemId, request.Body, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class DeletePlatformNavigationMenuItemHandler
    : IRequestHandler<DeletePlatformNavigationMenuItemCommand, Result<bool>>
{
    private readonly INavigationMenuAdminService _navigationMenuAdmin;

    public DeletePlatformNavigationMenuItemHandler(INavigationMenuAdminService navigationMenuAdmin) =>
        _navigationMenuAdmin = navigationMenuAdmin;

    public async Task<Result<bool>> Handle(DeletePlatformNavigationMenuItemCommand request, CancellationToken ct)
    {
        var (ok, err) = await _navigationMenuAdmin.DeleteNavItemAsync(request.ItemId, ct);
        return ok ? Result<bool>.Success(true) : Result<bool>.Failure(err ?? "Error");
    }
}

public sealed class RevokePlatformUserSessionsHandler : IRequestHandler<RevokePlatformUserSessionsCommand, Result<string>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IRefreshTokenService _refreshTokenService;

    public RevokePlatformUserSessionsHandler(
        IAccessRepository accessRepository,
        IRefreshTokenService refreshTokenService)
    {
        _accessRepository = accessRepository;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<string>> Handle(RevokePlatformUserSessionsCommand request, CancellationToken ct)
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

internal static class PlatformGrowthRangeParser
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
