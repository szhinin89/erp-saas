using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>SuperAdmin: menú efectivo por empresa y CRUD de <c>subscriber_custom_menus</c>.</summary>
public interface ISubscriberMenuAdminService
{
    Task<Result<TenantMenuResolvedDto>> GetResolvedMenuForTenantAsync(Guid subscriberId, CancellationToken ct = default);

    Task<Result<object?>> UpsertSubscriberCustomMenuAsync(Guid subscriberId, string menuConfigJson, CancellationToken ct = default);

    Task<Result<object?>> DeleteSubscriberCustomMenuAsync(Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> GetSubscriberIdsWithCustomMenuAsync(CancellationToken ct = default);
}

public sealed record TenantMenuResolvedDto(
    IReadOnlyList<SessionMenuGroupDto> Menu,
    bool HasCustomMenu,
    bool UsedPlanMenu,
    bool UsedGlobalFallback);
