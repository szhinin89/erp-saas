using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.AssignTemporaryPasswordAdmin;
using ERP.Application.Access.UseCases.CreateSystemUserAdmin;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Único punto de creación de un <see cref="ERP.Domain.Access.Entities.IdentityUser"/> nuevo fuera
/// del flujo de First Run. Permiso propio (<see cref="AccessPermissions.IdentityUsersCreate"/>) en
/// un controller separado de <see cref="CompanyUserMembershipsController"/> — los atributos
/// [Authorize] de clase y de método se combinan con AND en ASP.NET Core, así que exigir aquí
/// también MembershipsView habría requerido ambos permisos a la vez. TenantId/CompanyId nunca
/// viajan en el request: <see cref="CreateSystemUserAdminHandler"/> los resuelve del contexto
/// autenticado, mismo principio anti-IDOR que <see cref="CompanyUserMembershipsController"/>.
/// </summary>
[ApiController]
[Route("api/v1/admin/iam/users")]
[Authorize(Policy = $"perm:{AccessPermissions.IdentityUsersCreate}")]
[Produces("application/json")]
public sealed class IdentityUsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public IdentityUsersController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateSystemUserResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSystemUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateSystemUserAdminCommand(
            request.Username,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.Role,
            request.ProfileId
        );

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Asigna una contraseña temporal a un usuario existente de la empresa activa. Fuerza
    /// RequirePasswordReset y revoca todo su acceso vivo (refresh tokens + sesiones activas).
    /// Nunca devuelve la contraseña. Permiso propio (más restrictivo que IdentityUsersCreate,
    /// mismo patrón AND de clase+método que AdminUserSessionController.Close).
    /// </summary>
    [HttpPost("{username}/assign-temporary-password")]
    [Authorize(Policy = $"perm:{AccessPermissions.IdentityUsersAssignTemporaryPassword}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignTemporaryPassword(
        string username,
        [FromBody] AssignTemporaryPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new AssignTemporaryPasswordAdminCommand(username, request.TemporaryPassword);
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

/// <summary>Body de POST — CompanyId/TenantId nunca viajan aquí, se resuelven del contexto autenticado.</summary>
public sealed record CreateSystemUserRequest(
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string Password,
    string Role,
    Guid? ProfileId = null
);

/// <summary>Body de POST assign-temporary-password. La contraseña la escribe el administrador a
/// mano hoy; cuando exista envío por correo, este campo pasará a opcional en el Command (no aquí,
/// para no filtrar ese detalle al contrato HTTP antes de tiempo).</summary>
public sealed record AssignTemporaryPasswordRequest(string TemporaryPassword);
