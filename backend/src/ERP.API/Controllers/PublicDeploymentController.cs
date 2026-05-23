using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Configuración pública mínima del despliegue (sin secretos). Usada por el SPA antes del login.
/// </summary>
[ApiController]
[AppFeature("Public Deployment API", "perm:public.deployment.api", "🧩", null, null, 992, IsVisibleInMenu = false)]
[Route("api/public")]
[Produces("application/json")]
public sealed class PublicDeploymentController : ControllerBase
{
    private readonly IDeploymentFeatureFlags _deployment;

    public PublicDeploymentController(IDeploymentFeatureFlags deployment) =>
        _deployment = deployment;

    /// <summary>Indica si el panel platform está habilitado en esta instancia.</summary>
    [HttpGet("deployment")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DeploymentInfoDto>), StatusCodes.Status200OK)]
    public IActionResult GetDeployment()
        => this.ApiOk(
            new DeploymentInfoDto(
                PlatformPanelEnabled: _deployment.IsPlatformPanelEnabled,
                MaxActiveSubscribers: _deployment.MaxActiveSubscribers,
                MaxIdentityUsers: _deployment.MaxIdentityUsers,
                DedicatedSingleClientInstance: _deployment.IsDedicatedSingleClientInstance,
                MaxUsersPerSubscriber: _deployment.MaxUsersPerSubscriber));
}

/// <param name="PlatformPanelEnabled">Alias legacy JSON — mismo valor que <see cref="PlatformPanelEnabled"/>.</param>
public sealed record DeploymentInfoDto(
    bool PlatformPanelEnabled,
    int? MaxActiveSubscribers,
    int? MaxIdentityUsers,
    bool DedicatedSingleClientInstance,
    int? MaxUsersPerSubscriber)
{
    /// <summary>Flag canónico para el SPA (preferir en clientes nuevos).</summary>
    public bool PlatformPanelEnabled => PlatformPanelEnabled;
}
