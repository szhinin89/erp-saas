using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Configuration.DTOs;
using ERP.Application.Modules.Configuration.UseCases.GetConfigurationChangeLog;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: endpoint mínimo de solo lectura sobre ConfigurationChangeLog — no
/// hay UI de historial todavía (fuera de alcance de este bloque), pero el historial debe ser
/// consultable por soporte/auditoría. Gateado con SettingsPermissions.CompaniesUpdate (el mismo
/// permiso administrativo que ya protege escribir las configuraciones que este endpoint audita)
/// — no se inventa un permiso nuevo.
/// </summary>
[ApiController]
[Route("api/v1/configuration")]
[Authorize]
[Produces("application/json")]
public sealed class ConfigurationChangeLogController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConfigurationChangeLogController(IMediator mediator) => _mediator = mediator;

    [HttpGet("change-log")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<ConfigurationChangeLogPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangeLog(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? key,
        [FromQuery] OrgScope? scope,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetConfigurationChangeLogQuery(entityType, entityId, key, scope, from, to, page, pageSize),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
