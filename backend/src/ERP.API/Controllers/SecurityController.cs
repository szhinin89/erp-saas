using ERP.API.Controllers.Platform;
using MediatR;
using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Security.UseCases.GetSecurityAdminMatrix;
using ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Configuración de seguridad (matrices de delegación y permisos).
/// Acceso restringido: solo SuperAdmin.
/// </summary>
[ApiController]
[AppFeature("Security API", "perm:security.api", "🧩", null, null, 989, IsVisibleInMenu = false)]
[Route("api/[controller]")]
[Authorize(Policy = "Session")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
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
    public async Task<IActionResult> GetAdminMatrix(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSecurityAdminMatrixQuery(), ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(result.Error ?? "Error");

        return this.ApiOk(new
        {
            users = result.Value.Users,
            assignments = result.Value.Assignments
        });
    }

    /// <summary>Upsert de scopes de administración por sujeto (Role/User).</summary>
    [HttpPut("admin-scopes")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertAdminScopes([FromBody] UpsertSecurityAdminScopesCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}
