using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01. Nunca acepta tenantId/companyId desde body/query — siempre
/// del contexto autenticado (JWT vía ICurrentTenant/ICurrentCompany, resuelto dentro de los
/// handlers). No expone endpoint de unlock: una policy bloqueada (IsLocked=true) solo puede
/// desbloquearse por soporte fuera de este ticket.
/// </summary>
[ApiController]
[Route("api/v1/config/precision-policy")]
[Authorize]
[Produces("application/json")]
public sealed class CompanyPrecisionPolicyController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyPrecisionPolicyController(IMediator mediator) => _mediator = mediator;

    /// <summary>Devuelve la política de precisión numérica efectiva de la empresa activa.</summary>
    /// <remarks>
    /// Si la empresa ya registró operaciones reales (ventas, compras, inventario, pagos o
    /// contabilidad) la política se informa bloqueada (<c>isLocked</c>) aunque el bloqueo aún no se
    /// haya persistido. Esta consulta nunca escribe.
    /// </remarks>
    /// <response code="200">Política efectiva de la empresa.</response>
    /// <response code="404">La empresa no tiene política de precisión configurada.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<EffectivePrecisionPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetCompanyPrecisionPolicyQuery(), ct), "OK");

    /// <summary>Definiciones (keys/rangos/defaults) y perfiles predefinidos — metadata estática.</summary>
    /// <response code="200">Campos configurables con su rango y los valores de cada perfil predefinido.</response>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(ApiResponse<PrecisionPolicyMetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetadata(CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPrecisionPolicyMetadataQuery(), ct),
            "OK"
        );

    // SECURITY-PRECISION-POLICY-01: escritura de configuración de la empresa activa — mismo permiso
    // que el resto de escrituras de Datos de la empresa y que la UI. Los GET siguen solo autenticados
    // porque todos los módulos consumen la política efectiva.
    /// <summary>Actualiza el perfil de precisión numérica de la empresa activa.</summary>
    /// <remarks>
    /// <c>profileType</c> admite <c>StandardCommercial</c>, <c>HighPrecision</c> y <c>Custom</c>; solo con
    /// <c>Custom</c> se aplican los decimales enviados, los otros perfiles usan sus valores fijos. Cada
    /// valor se valida contra el rango de la metadata y la tolerancia de cuadre admite como máximo los
    /// decimales de un monto. Si la empresa ya registró operaciones reales, la política queda bloqueada
    /// de forma persistente y el cambio se rechaza con 409; no hay operación de desbloqueo.
    /// </remarks>
    /// <param name="cmd">Perfil y valores de precisión.</param>
    /// <response code="200">Política actualizada; devuelve la política efectiva.</response>
    /// <response code="404">La empresa no tiene política de precisión configurada.</response>
    /// <response code="409">La política está bloqueada o la empresa ya inició operaciones.</response>
    /// <response code="422">Perfil desconocido o valores fuera de rango.</response>
    [HttpPut]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<EffectivePrecisionPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCompanyPrecisionPolicyCommand cmd,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
}
