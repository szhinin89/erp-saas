using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Security.UseCases.GetSecurityAdminMatrix;
using ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Delegación de administración (matriz manageRoles/manageModules/manageScreens/manageProcesses
/// por usuario) — ver /admin/security. ADMIN-SECURITY-SPLIT-01: protegido por el sistema de
/// políticas perm: igual que el resto del ERP, ya no por [Authorize(Roles = SecurityRoles.Admin)]
/// como única puerta — ese chequeo por rol literal era la excepción del grupo Administración
/// (los otros cinco controllers ya usaban perm:). El rol Admin sigue teniendo acceso total: el
/// authorizer de "perm:" hace bypass automático para SecurityRoles.Admin (mismo mecanismo que
/// NavigationBuilder usa para el menú), así que no se pierde compatibilidad con el rol.
/// </summary>
[ApiController]
[AppFeature(
    "Delegación de administración (API)",
    $"perm:{AdminPermissions.DelegationView}",
    "🧩",
    null,
    null,
    989,
    IsVisibleInMenu = false
)]
[Route("api/v1/[controller]")]
[Authorize(Policy = "Session")]
[Authorize(Policy = $"perm:{AdminPermissions.DelegationView}")]
[Produces("application/json")]
public class SecurityController : ControllerBase
{
    private readonly IMediator _mediator;

    public SecurityController(IMediator mediator) => _mediator = mediator;

    /// <summary>Retorna usuarios del tenant + asignaciones actuales de scopes de administración.</summary>
    [HttpGet("admin-matrix")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminMatrix(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSecurityAdminMatrixQuery(), cancellationToken);
        if (!result.IsSuccess)
            return this.ApiBadRequest(result.Error ?? "Error");

        return this.ApiOk(
            new { users = result.Value.Users, assignments = result.Value.Assignments }
        );
    }

    /// <summary>Upsert de scopes de administración por sujeto (Role/User). Requiere
    /// admin.delegation.configure (más restrictivo que la policy del controller, que solo exige
    /// admin.delegation.view).</summary>
    [HttpPut("admin-scopes")]
    [Authorize(Policy = $"perm:{AdminPermissions.DelegationConfigure}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertAdminScopes(
        [FromBody] UpsertSecurityAdminScopesCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
