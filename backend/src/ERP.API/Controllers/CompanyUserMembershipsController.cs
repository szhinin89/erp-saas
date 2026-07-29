using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserMembershipsAdmin;
using ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Fase I-A — wiring administrativo de <c>CompanyUserMembership</c>: expone los UseCases de Fase D
/// (<see cref="UpsertCompanyUserMembershipAdminCommand"/>/<see cref="RevokeCompanyUserMembershipAdminCommand"/>,
/// ya probados) que hasta ahora no tenían ningún Controller/ruta — quedaban sin uso en producción.
/// TenantId/CompanyId nunca viajan en el request: cada Admin command los resuelve del contexto
/// autenticado (ver los handlers), nunca del body — mismo principio anti-IDOR documentado en
/// docs/IDENTITY.md a raíz de la eliminación del antiguo UserSessionController self-service.
/// Reutiliza access.company_user_memberships.view (mismo criterio que AccessProfilesController,
/// que ya usa ProfilesView tanto para lectura como para mutación de profiles, y que
/// CompanyUserPreferencesController, que reutiliza MembershipsView para su PUT). No se introduce
/// un permiso .manage nuevo en esta fase — ver docs/STATUS.md para la decisión documentada.
/// CompanyUserBranch y CompanyUserPreferences siguen siendo fuente única de verdad de sus propios
/// datos: este controller no las toca directamente, solo reenvía al UseCase de Fase D que ya las
/// orquesta vía MediatR.
/// </summary>
[ApiController]
[Route("api/v1/admin/iam/memberships")]
[Authorize(Policy = $"perm:{AccessPermissions.MembershipsView}")]
[Produces("application/json")]
public sealed class CompanyUserMembershipsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyUserMembershipsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Fase I-C — bloqueo real: no existía ningún endpoint que listara CompanyUserMembership de la
    /// empresa activa (con inactivas y ProfileName), necesario para la pantalla /access/users. Ver
    /// GetCompanyUserMembershipsAdminQuery.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CompanyUserMembershipAdminDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] bool onlyActive = false,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetCompanyUserMembershipsAdminQuery(onlyActive),
            cancellationToken
        );
        return this.ToOkOrBadRequest(
            result,
            successFallbackFactory: () => Array.Empty<CompanyUserMembershipAdminDto>()
        );
    }

    /// <summary>
    /// Fase F (renombrado a username en Fase G) — único punto de entrada de "Agregar usuario" en
    /// el frontend: resuelve si el username ya es un IdentityUser (y si ya tiene membership en
    /// esta empresa) antes de decidir entre reutilizar Upsert (existente) o CreateSystemUser
    /// (nuevo). Solo lectura, mismo permiso que el resto del controller.
    /// </summary>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(ApiResponse<UsernameLookupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Lookup(
        [FromQuery] string username,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new LookupUserByUsernameAdminQuery(username),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertCompanyUserMembershipRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpsertCompanyUserMembershipAdminCommand(
            request.Username,
            request.Role,
            request.ProfileId,
            request.AuthorizedBranchIds,
            request.DefaultBranchId,
            request.LoginMode
        );

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result, successFallbackFactory: () => new { });
    }

    [HttpPost("revoke")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        [FromBody] RevokeCompanyUserMembershipRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new RevokeCompanyUserMembershipAdminCommand(request.Username);
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result, successFallbackFactory: () => new { });
    }
}

/// <summary>Body de POST — CompanyId/TenantId nunca viajan aquí, se resuelven del contexto autenticado.</summary>
public sealed record UpsertCompanyUserMembershipRequest(
    string Username,
    string Role,
    Guid? ProfileId = null,
    IReadOnlyList<Guid>? AuthorizedBranchIds = null,
    Guid? DefaultBranchId = null,
    string? LoginMode = null
);

/// <summary>Body de POST revoke — CompanyId/TenantId nunca viajan aquí.</summary>
public sealed record RevokeCompanyUserMembershipRequest(string Username);
