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

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CompanyUserBranchesAdminDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid membershipId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCompanyUserBranchesAdminQuery(membershipId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<CompanyUserBranchesAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid membershipId, [FromBody] UpdateCompanyUserBranchesRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateCompanyUserBranchesAdminCommand(membershipId, request.AuthorizedBranchIds);
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

/// <summary>Body de PUT — MembershipId viene de la ruta, nunca del payload.</summary>
public sealed record UpdateCompanyUserBranchesRequest(IReadOnlyList<Guid> AuthorizedBranchIds);
