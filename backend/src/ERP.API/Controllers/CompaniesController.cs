using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.CreateCompany;
using ERP.Application.Modules.Companies.UseCases.GetCurrentCompany;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Empresa activa",
    $"perm:{SettingsPermissions.CompaniesView}",
    "🏢",
    "/settings/company",
    null,
    28,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/companies")]
[Produces("application/json")]
public sealed class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Obtiene el detalle de la empresa activa (la del contexto del usuario).
    /// </summary>
    /// <remarks>
    /// Si el contexto de empresa no es válido para el usuario, el guard de acceso responde con su
    /// propio código (alcance de empresa).
    /// </remarks>
    /// <response code="200">Detalle de la empresa activa.</response>
    /// <response code="400">La empresa del contexto no existe.</response>
    [HttpGet("current")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCurrentCompanyQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea una empresa en el tenant indicado (aprovisionamiento).
    /// </summary>
    /// <remarks>
    /// Solo para tokens de aprovisionamiento global (política `CompanyProvisioning`). El tenant destino
    /// debe existir y estar activo. Si se envía `initialAdministrator`, se crea el administrador
    /// inicial con una contraseña temporal que se devuelve **una sola vez** en `credentials` (la
    /// respuesta lleva `Cache-Control: no-store`); si el usuario o el correo ya existen se responde 409
    /// con un mensaje genérico, sin indicar cuál. El RUC debe tener 13 caracteres.
    /// </remarks>
    /// <param name="command">Tenant destino, RUC, razón social, nombre comercial y, opcionalmente, administrador inicial.</param>
    /// <response code="201">Empresa creada; incluye las credenciales temporales del administrador inicial si se solicitó.</response>
    /// <response code="400">El tenant destino no existe o está inactivo.</response>
    /// <response code="409">El usuario o el correo del administrador inicial no están disponibles.</response>
    /// <response code="422">Validación de campos (tenant, RUC, razón social, datos del administrador).</response>
    [HttpPost]
    [Authorize(Policy = "CompanyProvisioning")]
    [ProducesResponseType(typeof(ApiResponse<CompanyCreatedDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        // La respuesta puede incluir las credenciales temporales del administrador inicial.
        Response.Headers.CacheControl = "no-store";
        return this.ToCreatedOrBadRequest(result);
    }
}
