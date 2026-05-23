using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Platform.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>
/// Platform Layer — auditoría append-only del control plane.
/// Canonical successor of localStorage audit in SuperAdmin menu hub.
/// </summary>
[ApiController]
[Route("api/platform/audit")]
[Authorize(Roles = "SuperAdmin")]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformAuditController : ControllerBase
{
    private readonly IPlatformAuditReader _audit;

    public PlatformAuditController(IPlatformAuditReader audit) => _audit = audit;

    /// <summary>Lista eventos recientes de auditoría platform (más recientes primero).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 100,
        [FromQuery] Guid? subscriberId = null,
        CancellationToken ct = default)
    {
        var rows = await _audit.ListRecentAsync(limit, subscriberId, ct);
        return this.ApiOk(new { entries = rows });
    }
}
