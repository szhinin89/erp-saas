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
// IAM-V1-F2: cada acción declara su propio permiso — a nivel de clase solo se exige sesión
// autenticada. Antes la policy de clase (IdentityUsersCreate) se sumaba con AND a la de la
// contraseña temporal, que entonces exigía también permiso de crear usuarios.
// (Comentario normal, no XML: el summary de clase se publica como descripción del tag OpenAPI.)
[ApiController]
[Route("api/v1/admin/iam/users")]
[Authorize]
[Produces("application/json")]
public sealed class IdentityUsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public IdentityUsersController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Crea un usuario de identidad y su membresía en la empresa activa.
    /// </summary>
    /// <remarks>
    /// Alta en una sola transacción: crea el usuario y su membresía (rol y perfil opcional) con la
    /// misma lógica que el POST de membresías; si la membresía falla se revierte todo. El nombre de
    /// usuario se normaliza a minúsculas y, junto con el correo, es único en todo el sistema: un
    /// conflicto responde con un mensaje genérico que no indica cuál de los dos existe. El usuario
    /// queda obligado a cambiar la contraseña en su primer inicio de sesión. Para vincular una
    /// cuenta existente del tenant se usa el lookup y el POST de membresías, no este endpoint.
    /// Responde 200 (no 201) con el id y el username.
    /// </remarks>
    /// <param name="request">
    /// Username, nombres, correo opcional, contraseña inicial que cumpla la política de
    /// complejidad, rol y perfil opcional.
    /// </param>
    /// <response code="200">Usuario creado (identityUserId y username).</response>
    /// <response code="403">
    /// Sin autoridad para asignar el rol o el perfil (reglas de la membresía).
    /// </response>
    /// <response code="404">
    /// No existen el tenant o la empresa activa, o el perfil o alguna sucursal indicada.
    /// </response>
    /// <response code="409">El nombre de usuario o el correo no están disponibles.</response>
    /// <response code="422">
    /// Datos inválidos (username, nombres, correo o contraseña fuera de política), perfil requerido
    /// o inactivo.
    /// </response>
    [HttpPost]
    [Authorize(Policy = $"perm:{AccessPermissions.IdentityUsersCreate}")]
    [ProducesResponseType(typeof(ApiResponse<CreateSystemUserResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
    /// Asigna una contraseña temporal a un usuario de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Fuerza el cambio de contraseña en el próximo inicio de sesión y revoca todo su acceso vivo
    /// (tokens de refresco y sesiones activas). Nunca devuelve la contraseña. Solo aplica a
    /// usuarios con membresía en la empresa activa: una cuenta global, de otra empresa o
    /// inexistente responde 404. La contraseña debe cumplir la política de complejidad.
    /// </remarks>
    /// <param name="username">Nombre de usuario del destinatario.</param>
    /// <param name="request">Contraseña temporal.</param>
    /// <response code="200">Contraseña temporal asignada.</response>
    /// <response code="403">No hay una empresa activa en la sesión (alcance de empresa).</response>
    /// <response code="404">
    /// El usuario no existe, es global o no tiene acceso a la empresa activa.
    /// </response>
    /// <response code="422">
    /// Username vacío, o la contraseña vacía o fuera de la política de complejidad.
    /// </response>
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
