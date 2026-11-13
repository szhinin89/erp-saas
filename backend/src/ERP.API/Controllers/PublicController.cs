using ERP.API.Contracts;
using ERP.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Public configuration endpoint — no auth required. Consumed by the frontend to detect ERP mode.</summary>
[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class PublicController : ControllerBase
{
    /// <summary>
    /// Informa el modo de despliegue de la instalación.
    /// </summary>
    /// <remarks>
    /// Operación pública que consume el frontend antes de autenticarse. Devuelve valores fijos del despliegue:
    /// <c>platformPanelEnabled</c> (siempre falso), <c>dedicatedSingleClientInstance</c> (siempre verdadero) y los
    /// límites <c>maxActiveSubscribers</c>, <c>maxIdentityUsers</c> y <c>maxUsersPerSubscriber</c> (nulos, sin
    /// límite).
    /// </remarks>
    /// <response code="200">Datos del despliegue.</response>
    [HttpGet("deployment")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult GetDeploymentInfo() =>
        this.ApiOk(
            new
            {
                platformPanelEnabled = false,
                dedicatedSingleClientInstance = true,
                maxActiveSubscribers = (int?)null,
                maxIdentityUsers = (int?)null,
                maxUsersPerSubscriber = (int?)null,
            }
        );
}
