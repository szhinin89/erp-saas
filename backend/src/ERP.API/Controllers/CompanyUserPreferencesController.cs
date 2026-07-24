using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserPreferencesAdmin;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Fase F — administración explícita de CompanyUserPreferences (sucursal por defecto + modo de
/// login de un usuario dentro de la empresa activa). Reutiliza access.company_user_memberships.view
/// (mismo criterio que AccessProfilesController, que ya usa ese permiso tanto para lectura como
/// para mutación de profiles) — no se introduce un permiso nuevo. Sin lógica propia: cada acción
/// solo recibe el request, lo reenvía a Application y traduce el Result a HTTP.
/// </summary>
[ApiController]
[Route("api/v1/admin/iam/company-users/{companyUserId:guid}/preferences")]
[Authorize(Policy = $"perm:{AccessPermissions.MembershipsView}")]
[Produces("application/json")]
public sealed class CompanyUserPreferencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyUserPreferencesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CompanyUserPreferencesAdminDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid companyUserId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCompanyUserPreferencesAdminQuery(companyUserId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<CompanyUserPreferencesAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid companyUserId, [FromBody] UpdateCompanyUserPreferencesRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateCompanyUserPreferencesAdminCommand(companyUserId, request.LoginMode, request.DefaultBranchId);
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

/// <summary>Body de PUT — CompanyUserId viene de la ruta, nunca del payload.</summary>
public sealed record UpdateCompanyUserPreferencesRequest(string LoginMode, Guid? DefaultBranchId);
