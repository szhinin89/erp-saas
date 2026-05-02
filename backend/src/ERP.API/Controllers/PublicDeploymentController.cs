using ERP.API.Contracts;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Configuración pública mínima del despliegue (sin secretos). Usada por el SPA antes del login.
/// </summary>
[ApiController]
[Route("api/public")]
[Produces("application/json")]
public sealed class PublicDeploymentController : ControllerBase
{
    private readonly IDeploymentFeatureFlags _deployment;

    public PublicDeploymentController(IDeploymentFeatureFlags deployment) =>
        _deployment = deployment;

    /// <summary>Indica si el panel global SuperAdmin está habilitado en esta instancia.</summary>
    [HttpGet("deployment")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DeploymentInfoDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<DeploymentInfoDto>> GetDeployment() =>
        Ok(new ApiResponse<DeploymentInfoDto>(
            Success: true,
            Message: "OK",
            new DeploymentInfoDto(
                SuperAdminPanelEnabled: _deployment.IsSuperAdminPanelEnabled,
                MaxActiveTenants: _deployment.MaxActiveTenants,
                MaxIdentityUsers: _deployment.MaxIdentityUsers,
                DedicatedSingleClientInstance: _deployment.IsDedicatedSingleClientInstance,
                MaxUsersPerTenant: _deployment.MaxUsersPerTenant)));
}

public sealed record DeploymentInfoDto(
    bool SuperAdminPanelEnabled,
    int? MaxActiveTenants,
    int? MaxIdentityUsers,
    bool DedicatedSingleClientInstance,
    int? MaxUsersPerTenant);
