using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Catálogo público de planes (landing / precios externos). Sin autenticación.</summary>
[ApiController]
[Modulo("Public Plans API", "perm:public.plans.api", "🧩", null, null, 993, VisibleEnMenu = false)]
[Route("api/public")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class PublicPlansController : ControllerBase
{
    private readonly ISaasPublicPlansQuery _query;

    public PublicPlansController(ISaasPublicPlansQuery query) => _query = query;

    /// <summary>Planes activos y marcados como visibles públicamente, con features incluidas.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _query.GetVisiblePlansAsync(ct);
        return this.ApiOk(new { plans });
    }
}
