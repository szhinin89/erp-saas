using ERP.API.Contracts;
using ERP.Application.Audit.DTOs;
using ERP.Application.Audit.UseCases.GetMyActivity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ActivityController : ControllerBase
{
    private readonly GetMyActivityHandler _getMy;

    public ActivityController(GetMyActivityHandler getMy)
    {
        _getMy = getMy;
    }

    /// <summary>
    /// Historial del usuario autenticado (últimas acciones).
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(
        [FromQuery] string? module = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _getMy.HandleAsync(module, page, pageSize, ct);
        return Ok(new ApiResponse<IReadOnlyList<UserActivityDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<UserActivityDto>()));
    }
}

