using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSystemProviderSettings;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSystemProviderSettings;
using ERP.Domain.Kernel.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// ERP-CORE-CLOSEOUT-09 — datos del proveedor del sistema de facturación electrónica (quién
/// construyó/mantiene el software). Deliberadamente NO company-scoped: es un dato único por
/// instancia del ERP, distinto del emisor de cada comprobante (<c>Company</c>/<c>SriSettings</c>,
/// que sí son por empresa). Acceso: Admin del tenant únicamente — mismo patrón de autorización
/// que <see cref="SecurityController"/>.
/// </summary>
[AppFeature(
    "Proveedor de sistema",
    "perm:system.provider_settings",
    "🏷️",
    null,
    null,
    990,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/system/provider-settings")]
[Authorize(Policy = "Session")]
[Authorize(Roles = SecurityRoles.Admin)]
[Produces("application/json")]
public sealed class SystemProviderSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SystemProviderSettingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SystemProviderSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetSystemProviderSettingsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<SystemProviderSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertSystemProviderSettingsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
