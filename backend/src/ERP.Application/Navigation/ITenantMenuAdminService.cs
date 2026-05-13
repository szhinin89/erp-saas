using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>SuperAdmin: menú efectivo por empresa y CRUD de <c>tenant_custom_menus</c>.</summary>
public interface ITenantMenuAdminService
{
    Task<Result<TenantMenuResolvedDto>> GetResolvedMenuForTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<Result<object?>> UpsertTenantCustomMenuAsync(Guid tenantId, string menuConfigJson, CancellationToken ct = default);

    Task<Result<object?>> DeleteTenantCustomMenuAsync(Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> GetTenantIdsWithCustomMenuAsync(CancellationToken ct = default);
}

public sealed record TenantMenuResolvedDto(
    IReadOnlyList<SessionMenuGroupDto> Menu,
    bool HasCustomMenu,
    bool UsedPlanMenu,
    bool UsedGlobalFallback);
