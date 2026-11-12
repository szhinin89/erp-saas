using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.CompanyAdministratorsForAdminCore;
using ERP.Application.Modules.Company.UseCases.ListCompaniesForAdminCore;
using ERP.Application.Modules.Company.UseCases.UpdateCompanyForAdminCore;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// AdminGlobalCore: dashboard global de empresas por tenant. Solo accesible con token global
/// (tenant_id == Guid.Empty + rol Admin) — ver policy "PlatformAdmin" en Program.cs. Nunca
/// consumido por el ERP operativo (AppLayout/menú de tenant).
/// </summary>
[AppFeature(
    "Admin Core",
    "perm:admin_core.dashboard",
    "🌐",
    null,
    null,
    991,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/admin-core")]
[Authorize(Policy = "PlatformAdmin")]
[Produces("application/json")]
public sealed class AdminCoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminCoreController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista las empresas de todos los tenants para la consola global.
    /// </summary>
    /// <response code="200">Empresas registradas (posiblemente vacías).</response>
    [HttpGet("companies")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<AdminCoreCompanyDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> ListCompanies(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCompaniesForAdminCoreQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<AdminCoreCompanyDto>());
    }

    /// <summary>
    /// Lista los administradores empresariales de una empresa.
    /// </summary>
    /// <remarks>
    /// Solo incluye cuentas con membresía Admin activa en la empresa que no sean globales; nunca
    /// devuelve credenciales. Alimenta la acción «Generar nueva contraseña temporal».
    /// </remarks>
    /// <param name="companyId">Id de la empresa.</param>
    /// <response code="200">Administradores de la empresa.</response>
    /// <response code="403">El llamante no es administrador global.</response>
    /// <response code="404">La empresa no existe.</response>
    [HttpGet("companies/{companyId:guid}/administrators")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CompanyAdministratorDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListCompanyAdministrators(
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ListCompanyAdministratorsForAdminCoreQuery(companyId),
            cancellationToken
        );
        return this.ToOkOrNotFound(result);
    }

    /// <summary>
    /// Genera una nueva contraseña temporal para un administrador de empresa.
    /// </summary>
    /// <remarks>
    /// Invalida la contraseña anterior, exige cambiarla en el próximo ingreso y revoca todo su
    /// acceso vivo (tokens de refresco y sesiones). La respuesta es la única vez que se muestra la
    /// contraseña temporal y se envía con Cache-Control no-store. No es idempotente: cada llamada
    /// genera una contraseña distinta. Solo aplica a administradores no globales con membresía
    /// Admin activa en la empresa.
    /// </remarks>
    /// <param name="companyId">Id de la empresa.</param>
    /// <param name="identityUserId">Id del usuario administrador.</param>
    /// <response code="200">
    /// Contraseña temporal generada (visible solo en esta respuesta).
    /// </response>
    /// <response code="403">El llamante no es administrador global.</response>
    /// <response code="404">
    /// La empresa no existe, o el administrador no existe, es global, no tiene rol Admin activo o
    /// es de otra empresa.
    /// </response>
    [HttpPost("companies/{companyId:guid}/administrators/{identityUserId:guid}/temporary-password")]
    [ProducesResponseType(
        typeof(ApiResponse<RegeneratedAdministratorPasswordDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateAdministratorPassword(
        Guid companyId,
        Guid identityUserId,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new RegenerateCompanyAdministratorPasswordCommand(companyId, identityUserId),
            cancellationToken
        );
        // Respuesta con credenciales: nunca en caché de navegador ni de proxy.
        Response.Headers.CacheControl = "no-store";
        return this.ToOkOrNotFound(result);
    }

    /// <summary>
    /// Edita la identidad de una empresa desde la consola global.
    /// </summary>
    /// <remarks>
    /// Actualiza RUC, razón social, nombre comercial y estado (isActive) de una empresa de
    /// cualquier tenant; el RUC y el estado solo se editan por esta vía. Razón social y nombre
    /// comercial siguen las mismas reglas que el perfil de la empresa activa.
    /// </remarks>
    /// <param name="id">Id de la empresa; debe coincidir con el del cuerpo.</param>
    /// <param name="command">Id, razón social, nombre comercial, estado y RUC.</param>
    /// <response code="200">Empresa actualizada.</response>
    /// <response code="400">El id de la ruta no coincide con el del cuerpo.</response>
    /// <response code="403">El llamante no es administrador global.</response>
    /// <response code="404">La empresa no existe.</response>
    /// <response code="409">Otra empresa ya usa ese RUC.</response>
    /// <response code="422">
    /// RUC ecuatoriano inválido, razón social obligatoria o nombres que exceden el máximo de 200
    /// caracteres.
    /// </response>
    [HttpPut("companies/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCompany(
        Guid id,
        [FromBody] UpdateCompanyForAdminCoreCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
