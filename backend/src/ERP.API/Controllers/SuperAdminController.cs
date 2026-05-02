using ERP.API.Contracts;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
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
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]
[Produces("application/json")]
public class SuperAdminController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISaasCatalogQuery _saasCatalogQuery;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly InstanceQuotaFileStore _instanceQuotaFile;

    public SuperAdminController(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        ISaasCatalogQuery saasCatalogQuery,
        IDeploymentFeatureFlags deployment,
        InstanceQuotaFileStore instanceQuotaFile)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _saasCatalogQuery = saasCatalogQuery;
        _deployment = deployment;
        _instanceQuotaFile = instanceQuotaFile;
    }

    /// <summary>Cuotas efectivas de la instancia (config + archivo <c>App_Data/instance-quota.json</c> si existe).</summary>
    [HttpGet("instance-quota")]
    [ProducesResponseType(typeof(ApiResponse<InstanceQuotaFileModel>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<InstanceQuotaFileModel>> GetInstanceQuota()
    {
        var dto = new InstanceQuotaFileModel
        {
            DedicatedSingleClientInstance = _deployment.IsDedicatedSingleClientInstance,
            MaxActiveTenants = _deployment.MaxActiveTenants,
            MaxIdentityUsers = _deployment.MaxIdentityUsers,
            MaxUsersPerTenant = _deployment.MaxUsersPerTenant,
        };
        return Ok(new ApiResponse<InstanceQuotaFileModel>(true, "OK", dto));
    }

    /// <summary>
    /// Guarda cuotas en <c>App_Data/instance-quota.json</c> (sobrescribe configuración para esos campos).
    /// En modo dedicado a un cliente, <c>maxActiveTenants</c> debe ser &gt; 0 (no se permiten empresas/RUC ilimitadas).
    /// </summary>
    [HttpPut("instance-quota")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult PutInstanceQuota([FromBody] InstanceQuotaFileModel body)
    {
        if (body.DedicatedSingleClientInstance == true &&
            (!body.MaxActiveTenants.HasValue || body.MaxActiveTenants <= 0))
        {
            return BadRequest(new ApiResponse<object>(
                false,
                "En instancia dedicada debe indicar maxActiveTenants (máximo de empresas/RUC) mayor que cero.",
                new { }));
        }

        _instanceQuotaFile.Write(body);
        return Ok(new ApiResponse<object>(true, "Guardado", new { }));
    }

    /// <summary>Catálogo de planes comerciales y features incluidas (solo lectura).</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlansCatalog(CancellationToken ct)
    {
        var plans = await _saasCatalogQuery.GetPlansWithFeaturesAsync(ct);
        return Ok(new ApiResponse<object>(true, "OK", new { plans }));
    }

    /// <summary>Lista todas las empresas (tenants) activas.</summary>
    /// <remarks>
    /// Útil para el "Tenant Picker" del Panel Global de SuperAdmin.
    /// </remarks>
    /// <response code="200">Lista de empresas activas (id, name, slug).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    /// <response code="403">El usuario no tiene rol SuperAdmin.</response>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var tenants = await _tenantRepository.GetAllAsync(ct);
        var active = tenants.Where(t => t.IsActive).ToList();

        // Métricas por empresa (globales del tenant): usuarios total/activos.
        // Nota: usamos ejecución SECUENCIAL para evitar concurrencia sobre el mismo DbContext scoped.
        // Si este endpoint crece en carga, se optimiza con queries agregadas (COUNT/GROUP BY) a nivel DB.
        var items = new List<object>(active.Count);
        foreach (var t in active)
        {
            var users = await _userRepository.GetAllByTenantAsync(t.Id, ct);
            var totalUsers = users.Count;
            var activeUsers = users.Count(u => u.IsActive);
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
                enabledModules = TenantSubscriptionCatalog.GetEffectiveEnabledModules(t),
                hasModuleRestrictions = !string.IsNullOrWhiteSpace(t.EnabledModulesJson),
            });
        }

        return Ok(new ApiResponse<object>(true, "OK", new { tenants = items }));
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

        return Ok(new ApiResponse<object>(true, "OK", new
        {
            totals = new
            {
                totalTenants,
                activeTenants,
                totalUsers,
                activeUsers
            },
            recentTenants
        }));
    }
}

