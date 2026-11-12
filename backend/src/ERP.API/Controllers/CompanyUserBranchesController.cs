using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserBranchesAdmin;
using ERP.Application.Access.UseCases.UpdateCompanyUserBranchesAdmin;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Fase I-B — administración explícita de CompanyUserBranch (sucursales autorizadas de una
/// CompanyUserMembership). CompanyUserBranch sigue siendo la única fuente de verdad de esa
/// autorización — este controller no la duplica en Membership/Preferences/IdentityUser, solo la
/// expone. TenantId/CompanyId nunca viajan en el request: cada UseCase los resuelve del contexto
/// autenticado. Reutiliza access.company_user_memberships.view (mismo criterio que
/// CompanyUserPreferencesController/AccessProfilesController/CompanyUserMembershipsController) —
/// no se introduce un permiso nuevo en esta fase.
/// </summary>
[ApiController]
[Route("api/v1/admin/iam/memberships/{membershipId:guid}/branches")]
[Authorize(Policy = $"perm:{AccessPermissions.MembershipsView}")]
[Produces("application/json")]
public sealed class CompanyUserBranchesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyUserBranchesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Sucursales activas de la empresa y cuáles tiene autorizadas un usuario de empresa.
    /// </summary>
    /// <remarks>
    /// Devuelve cada sucursal activa de la empresa con el indicador authorized para la membresía.
    /// Una membresía inexistente, de otra empresa o de una cuenta global responde 404.
    /// </remarks>
    /// <param name="membershipId">Id de la membresía (usuario de empresa).</param>
    /// <response code="200">Sucursales con su indicador de autorización.</response>
    /// <response code="404">
    /// La membresía no existe, es de otra empresa o de una cuenta global.
    /// </response>
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyUserBranchesAdminDto?>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute] Guid membershipId,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new GetCompanyUserBranchesAdminQuery(membershipId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reemplaza las sucursales autorizadas de un usuario de empresa.
    /// </summary>
    /// <remarks>
    /// Todo o nada: si alguna sucursal no existe, es de otra empresa o está inactiva no se guarda
    /// ningún cambio. Las sucursales incluidas se activan y las no incluidas se desactivan (no se
    /// eliminan); una lista vacía revoca todas sin desactivar la membresía. Repetir la misma lista
    /// no cambia el resultado. Una membresía revocada no admite cambios.
    /// </remarks>
    /// <param name="membershipId">Id de la membresía (usuario de empresa).</param>
    /// <param name="request">
    /// Lista completa de ids de sucursales autorizadas (puede estar vacía).
    /// </param>
    /// <response code="200">Sucursales autorizadas actualizadas.</response>
    /// <response code="403">La membresía está revocada.</response>
    /// <response code="404">
    /// La membresía no existe, es de otra empresa o de una cuenta global.
    /// </response>
    /// <response code="422">
    /// Lista ausente, id de sucursal vacío, o sucursal inexistente, de otra empresa o inactiva.
    /// </response>
    [HttpPut]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyUserBranchesAdminDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid membershipId,
        [FromBody] UpdateCompanyUserBranchesRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateCompanyUserBranchesAdminCommand(
            membershipId,
            request.AuthorizedBranchIds
        );
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

/// <summary>Body de PUT — MembershipId viene de la ruta, nunca del payload.</summary>
public sealed record UpdateCompanyUserBranchesRequest(IReadOnlyList<Guid> AuthorizedBranchIds);
