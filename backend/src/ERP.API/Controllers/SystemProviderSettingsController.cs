using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSystemProviderSettings;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSystemProviderSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// ERP-CORE-CLOSEOUT-09 — datos del proveedor del sistema de facturación electrónica (quién
/// construyó/mantiene el software). Deliberadamente NO company-scoped: es un dato único por
/// instancia del ERP, distinto del emisor de cada comprobante (<c>Company</c>/<c>SriSettings</c>,
/// que sí son por empresa). Acceso: solo AdminGlobalCore (tenant_id == Guid.Empty + rol Admin) —
/// un Admin de tenant/empresa normal NO debe leer ni modificar este singleton compartido por
/// todas las empresas del SaaS. Ver policy "PlatformAdmin" en Program.cs.
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
[Authorize(Policy = "PlatformAdmin")]
[Produces("application/json")]
public sealed class SystemProviderSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SystemProviderSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Devuelve los datos del proveedor del sistema de facturación electrónica.
    /// </summary>
    /// <remarks>
    /// Es un dato único por instancia del ERP, no por empresa (distinto del emisor de cada
    /// comprobante). Si aún no se configuró devuelve el registro vacío: sin RUC, razón social ni
    /// CIIU, deshabilitado y no configurado por completo.
    /// </remarks>
    /// <response code="200">Configuración vigente del proveedor del sistema.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SystemProviderSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetSystemProviderSettingsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea o actualiza los datos del proveedor del sistema de facturación electrónica.
    /// </summary>
    /// <remarks>
    /// Reemplaza el único registro de la instancia. Habilitar el proveedor exige RUC, razón social
    /// y CIIU completos y una fecha de vigencia; el RUC debe tener 13 dígitos numéricos, la razón
    /// social hasta 300 caracteres y el CIIU hasta 20.
    /// </remarks>
    /// <param name="command">
    /// RUC, razón social, código CIIU, fecha de vigencia (DateOnly, YYYY-MM-DD) y si está
    /// habilitado.
    /// </param>
    /// <response code="200">Configuración guardada.</response>
    /// <response code="422">
    /// RUC, razón social o CIIU inválidos, o habilitado sin los datos completos o sin fecha de
    /// vigencia.
    /// </response>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<SystemProviderSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertSystemProviderSettingsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
