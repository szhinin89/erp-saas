using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Security.UseCases.GetSecurityAdminMatrix;
using ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;
using ERP.Application.Security.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Configuración de seguridad (matrices de delegación y permisos).
/// Acceso restringido: solo SuperAdmin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
[Produces("application/json")]
public class SecurityController : ControllerBase
{
    private readonly GetSecurityAdminMatrixHandler _getAdminMatrixHandler;
    private readonly UpsertSecurityAdminScopesHandler _upsertAdminScopesHandler;

    public SecurityController(
        GetSecurityAdminMatrixHandler getAdminMatrixHandler,
        UpsertSecurityAdminScopesHandler upsertAdminScopesHandler)
    {
        _getAdminMatrixHandler = getAdminMatrixHandler;
        _upsertAdminScopesHandler = upsertAdminScopesHandler;
    }

    /// <summary>
    /// Retorna usuarios del tenant + asignaciones actuales de scopes de administración.
    /// </summary>
    /// <remarks>
    /// Esta matriz define "quién puede administrar" (delegación) y se usa para habilitar
    /// funciones como: gestionar roles, módulos, pantallas y procesos (y a futuro, secciones/campos).
    /// </remarks>
    /// <response code="200">Usuarios del tenant y asignaciones por sujeto (User/Role).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    /// <response code="403">El usuario no tiene rol SuperAdmin.</response>
    [HttpGet("admin-matrix")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminMatrix(CancellationToken ct)
    {
        var result = await _getAdminMatrixHandler.HandleAsync(ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(result.Error ?? "Error");

        return this.ApiOk(new
        {
            users = result.Value.Users,
            assignments = result.Value.Assignments
        });
    }

    /// <summary>
    /// Upsert de scopes de administración por sujeto (Role/User).
    /// </summary>
    /// <remarks>
    /// Reemplaza el set de scopes para el sujeto enviado. Idempotente:
    /// si envías el mismo payload, el resultado final no cambia.
    /// </remarks>
    /// <response code="200">Scopes guardados correctamente.</response>
    /// <response code="400">Error de validación o sujeto inválido.</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    /// <response code="403">El usuario no tiene rol SuperAdmin.</response>
    [HttpPut("admin-scopes")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertAdminScopes([FromBody] UpsertSecurityAdminScopesCommand command, CancellationToken ct)
    {
        var result = await _upsertAdminScopesHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}

