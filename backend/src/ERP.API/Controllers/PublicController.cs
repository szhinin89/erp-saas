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
    [HttpGet("deployment")]
    public IActionResult GetDeploymentInfo()
        => this.ApiOk(new
        {
            platformPanelEnabled = false,
            dedicatedSingleClientInstance = true,
            maxActiveSubscribers = (int?)null,
            maxIdentityUsers = (int?)null,
            maxUsersPerSubscriber = (int?)null,
        });
}
