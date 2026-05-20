using System.Globalization;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Subscriptions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using ERP.Infrastructure.Deployment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Endpoints globales para SuperAdmin (multi-empresa).
/// Estos endpoints ignoran el contexto del tenant porque el SuperAdmin opera a nivel sistema.
/// </summary>
[ApiController]
[AppFeature("SuperAdmin Global", "perm:superadmin.global.admin", "🧩", null, null, 982, IsVisibleInMenu = false, IsSuperAdmin = true)]
[Route("api/superadmin")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public class SuperAdminController : ControllerBase
{
    private readonly ITenantRepository       _tenantRepository;
    private readonly IUserRepository         _userRepository;
    private readonly ISaasCatalogQuery       _saasCatalogQuery;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly InstanceQuotaFileStore  _instanceQuotaFile;
    private readonly INavigationMenuAdminService _navigationMenuAdmin;
    private readonly IGrowthAnalyticsReader  _growthAnalytics;
    private readonly IRefreshTokenService    _refreshTokenService;
    private readonly ITenantMenuAdminService   _tenantMenuAdmin;
    private readonly ISessionModulesResolver _sessionModules;

    public SuperAdminController(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        ISaasCatalogQuery saasCatalogQuery,
        IDeploymentFeatureFlags deployment,
        InstanceQuotaFileStore instanceQuotaFile,
        INavigationMenuAdminService navigationMenuAdmin,
        IGrowthAnalyticsReader growthAnalytics,
        IRefreshTokenService refreshTokenService,
        ITenantMenuAdminService tenantMenuAdmin,
        ISessionModulesResolver sessionModules)
    {
        _tenantRepository    = tenantRepository;
        _userRepository      = userRepository;
        _saasCatalogQuery    = saasCatalogQuery;
        _deployment          = deployment;
        _instanceQuotaFile   = instanceQuotaFile;
        _navigationMenuAdmin = navigationMenuAdmin;
        _growthAnalytics     = growthAnalytics;
        _refreshTokenService = refreshTokenService;
        _tenantMenuAdmin     = tenantMenuAdmin;
        _sessionModules      = sessionModules;
    }

    /// <summary>Cuotas efectivas de la instancia (config + archivo <c>App_Data/instance-quota.json</c> si existe).</summary>
    [HttpGet("instance-quota")]
    [ProducesResponseType(typeof(ApiResponse<InstanceQuotaFileModel>), StatusCodes.Status200OK)]
    public IActionResult GetInstanceQuota()
    {
        var dto = new InstanceQuotaFileModel
        {
            DedicatedSingleClientInstance = _deployment.IsDedicatedSingleClientInstance,
            MaxActiveTenants = _deployment.MaxActiveTenants,
            MaxIdentityUsers = _deployment.MaxIdentityUsers,
            MaxUsersPerTenant = _deployment.MaxUsersPerTenant,
        };
        return this.ApiOk(dto);
    }

    /// <summary>
    /// Guarda cuotas en <c>App_Data/instance-quota.json</c> (sobrescribe configuración para esos campos).
    /// En modo dedicado a un cliente, <c>maxActiveTenants</c> debe ser &gt; 0 (no se permiten empresas/RUC ilimitadas).
    /// </summary>
    [HttpPut("instance-quota")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult PutInstanceQuota([FromBody] InstanceQuotaFileModel body)
    {
        if (body.DedicatedSingleClientInstance == true &&
            (!body.MaxActiveTenants.HasValue || body.MaxActiveTenants <= 0))
        {
            return this.ApiBadRequest("En instancia dedicada debe indicar maxActiveTenants (máximo de empresas/RUC) mayor que cero.");
        }

        _instanceQuotaFile.Write(body);
        return this.ApiOk(new { }, "Guardado");
    }

    /// <summary>Catálogo de planes comerciales y features incluidas (solo lectura).</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlansCatalog(CancellationToken ct)
    {
        var plans = await _saasCatalogQuery.GetPlansWithFeaturesAsync(ct);
        return this.ApiOk(new { plans });
    }

    /// <summary>Lista todas las empresas (tenants), activas e inactivas.</summary>
    /// <remarks>
    /// Útil para el "Tenant Picker" del Panel Global de SuperAdmin.
    /// </remarks>
    /// <response code="200">Lista de empresas (id, name, slug, isActive, plan, módulos).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    /// <response code="403">El usuario no tiene rol SuperAdmin.</response>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var tenants = (await _tenantRepository.GetAllAsync(ct))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var withCustomMenu = await _tenantMenuAdmin.GetTenantIdsWithCustomMenuAsync(ct);

        // Métricas por empresa (globales del tenant): usuarios total/activos.
        // Nota: usamos ejecución SECUENCIAL para evitar concurrencia sobre el mismo DbContext scoped.
        // Si este endpoint crece en carga, se optimiza con queries agregadas (COUNT/GROUP BY) a nivel DB.
        var items = new List<object>(tenants.Count);
        foreach (var t in tenants)
        {
            var users = await _userRepository.GetAllByTenantAsync(t.Id, ct);
            var totalUsers = users.Count;
            var activeUsers = users.Count(u => u.IsActive);
            var modules = await _sessionModules.GetEnabledModuleKeysAsync(t.Id, ct);
            items.Add(new
            {
                t.Id,
                t.Name,
                t.Slug,
                t.IsActive,
                t.CreatedAt,
                totalUsers,
                activeUsers,
                planCode = t.PlanCode,
                enabledModules = modules,
                hasModuleRestrictions = TenantSubscriptionCatalog.HasModuleRestrictionsFromModules(modules),
                hasCustomMenu = withCustomMenu.Contains(t.Id),
            });
        }

        return this.ApiOk(new { tenants = items });
    }

    /// <summary>Métricas globales del sistema (todas las empresas).</summary>
    /// <remarks>
    /// Retorna totales de empresas y usuarios para el Dashboard Global de SuperAdmin.
    /// </remarks>
    /// <response code="200">Totales y lista reciente de empresas.</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    /// <response code="403">El usuario no tiene rol SuperAdmin.</response>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
    {
        var tenants = await _tenantRepository.GetAllAsync(ct);
        var activeTenants = tenants.Count(t => t.IsActive);
        var totalTenants = tenants.Count;

        var totalUsers = await _userRepository.CountAllSystemAsync(ct);
        var activeUsers = await _userRepository.CountActiveSystemAsync(ct);

        var recentTenants = tenants
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Select(t => new { t.Id, t.Name, t.Slug, t.IsActive, t.CreatedAt })
            .ToList();

        return this.ApiOk(new
        {
            totals = new
            {
                totalTenants,
                activeTenants,
                totalUsers,
                activeUsers
            },
            recentTenants
        });
    }

    /// <summary>
    /// Serie de crecimiento de empresas, usuarios (identidad) y membresías por periodo (día/semana/mes), con acumulados.
    /// Por defecto últimos 3 meses y granularidad mensual.
    /// </summary>
    [HttpGet("growth-analytics")]
    [ProducesResponseType(typeof(ApiResponse<GrowthAnalyticsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGrowthAnalytics(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var toUtc = ParseDateOrDefault(to, DateTime.UtcNow.Date);
        var fromUtc = ParseDateOrDefault(from, toUtc.AddMonths(-3));
        if ((toUtc - fromUtc).TotalDays > 800)
        {
            return this.ApiBadRequest("El rango máximo permitido es aproximadamente 24 meses.");
        }

        var g = (granularity ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(g))
        {
            var spanDays = (toUtc - fromUtc).TotalDays;
            g = spanDays > 120 ? "month" : spanDays > 35 ? "week" : "day";
        }

        var dto = await _growthAnalytics.GetSeriesAsync(fromUtc, toUtc, g, ct);
        return this.ApiOk(dto);
    }

    /// <summary>
    /// Mismo rango y agrupación que growth-analytics; serie en MRR mensual equivalente (plan actual, tenants activos).
    /// </summary>
    [HttpGet("growth-analytics-monetary")]
    [ProducesResponseType(typeof(ApiResponse<GrowthMonetaryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGrowthMonetary(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var toUtc = ParseDateOrDefault(to, DateTime.UtcNow.Date);
        var fromUtc = ParseDateOrDefault(from, toUtc.AddMonths(-3));
        if ((toUtc - fromUtc).TotalDays > 800)
        {
            return this.ApiBadRequest("El rango máximo permitido es aproximadamente 24 meses.");
        }

        var g = (granularity ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(g))
        {
            var spanDays = (toUtc - fromUtc).TotalDays;
            g = spanDays > 120 ? "month" : spanDays > 35 ? "week" : "day";
        }

        var dto = await _growthAnalytics.GetMonetarySeriesAsync(fromUtc, toUtc, g, ct);
        return this.ApiOk(dto);
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

    /// <summary>Árbol del menú principal (grupos e ítems recursivos) para configuración.</summary>
    [HttpGet("navigation-menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNavigationMenu(CancellationToken ct)
    {
        var menu = await _navigationMenuAdmin.GetMenuTreeAsync(ct);
        return this.ApiOk(new { menu });
    }

    /// <summary>Reordena grupos activos del menú principal (<c>ui_nav_groups.sort_order</c>).</summary>
    [HttpPut("navigation-menu/groups/reorder")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReorderNavigationGroups(
        [FromBody] ReorderNavigationGroupsRequest body,
        CancellationToken ct)
    {
        if (body.OrderedGroupIds is not { Count: > 0 })
            return this.ApiBadRequest("orderedGroupIds requerido.");

        var (ok, err) = await _navigationMenuAdmin.ReorderGroupsAsync(body.OrderedGroupIds, ct);
        if (!ok)
            return this.ApiBadRequest(err ?? "Error");

        return this.ApiOk(new { }, "Guardado");
    }

    /// <summary>Aplica árbol de ítems: jerarquía (<c>parent_item_id</c>) y orden entre hermanos.</summary>
    [HttpPut("navigation-menu/items/reorder-levels")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReorderNavigationItemLevels(
        [FromBody] ReorderNavigationItemLevelsRequest body,
        CancellationToken ct)
    {
        if (body.Levels is not { Count: > 0 })
            return this.ApiBadRequest("levels requerido.");

        var (ok, err) = await _navigationMenuAdmin.ReorderItemLevelsAsync(body.Levels, ct);
        if (!ok)
            return this.ApiBadRequest(err ?? "Error");

        return this.ApiOk(new { }, "Guardado");
    }

    /// <summary>Crea un ítem de menú (submenú o hoja) bajo un grupo, en la raíz o bajo un padre.</summary>
    [HttpPost("navigation-menu/items")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateNavigationMenuItem([FromBody] CreateNavItemRequest body, CancellationToken ct)
    {
        var (ok, newId, err) = await _navigationMenuAdmin.CreateNavItemAsync(body, ct);
        if (!ok || newId is null)
            return this.ApiBadRequest(err ?? "Error");

        return this.ApiOk(new { id = newId }, "Creado");
    }

    /// <summary>Actualiza un ítem de menú (etiqueta visible, ruta, permisos, vínculo a feature SaaS).</summary>
    [HttpPut("navigation-menu/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateNavigationMenuItem(Guid itemId, [FromBody] UpdateNavItemRequest body, CancellationToken ct)
    {
        var (ok, err) = await _navigationMenuAdmin.UpdateNavItemAsync(itemId, body, ct);
        if (!ok)
            return this.ApiBadRequest(err ?? "Error");

        return this.ApiOk(new { }, "Guardado");
    }

    /// <summary>Desactiva un ítem y todos sus descendientes (el menú deja de mostrarlos).</summary>
    [HttpDelete("navigation-menu/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteNavigationMenuItem(Guid itemId, CancellationToken ct)
    {
        var (ok, err) = await _navigationMenuAdmin.DeleteNavItemAsync(itemId, ct);
        if (!ok)
            return this.ApiBadRequest(err ?? "Error");

        return this.ApiOk(new { }, "Eliminado");
    }

    // ── Gestión de sesiones (refresh tokens) ─────────────────────────────

    /// <summary>
    /// Revoca todos los refresh tokens activos de un usuario.
    /// Usar cuando se desactiva un usuario, se detecta compromiso o se cambia de rol.
    /// </summary>
    /// <param name="userId">ID del usuario cuyos tokens serán revocados.</param>
    /// <param name="tenantId">Tenant del usuario.</param>
    [HttpDelete("users/{userId:guid}/sessions")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeUserSessions(
        Guid userId, [FromQuery] Guid tenantId, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdSystemAsync(userId, ct);
        if (user is null)
            return this.ApiBadRequest("Usuario no encontrado.");

        await _refreshTokenService.RevokeAllForUserAsync(userId, tenantId, "Revocación administrativa", ct);
        return this.ApiOk($"Sesiones del usuario {userId} revocadas.", "OK");
    }
}

