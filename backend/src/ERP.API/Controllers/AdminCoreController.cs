using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
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
    /// Administradores empresariales (no globales) de la empresa, para la acción «Generar nueva
    /// contraseña temporal». Nunca incluye credenciales.
    /// </summary>
    [HttpGet("companies/{companyId:guid}/administrators")]
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
    /// Regenera la contraseña temporal del administrador: invalida la anterior, exige cambio en el
    /// próximo ingreso y revoca sus sesiones. La respuesta es la única vez que se muestra.
    /// </summary>
    [HttpPost("companies/{companyId:guid}/administrators/{identityUserId:guid}/temporary-password")]
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

    [HttpPut("companies/{id:guid}")]
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
