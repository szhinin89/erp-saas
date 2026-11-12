using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Auth.UseCases.ChangeMyPassword;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/admin/iam")]
[Produces("application/json")]
public sealed class AccessSessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccessSessionController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Devuelve las claves de permiso efectivas del usuario autenticado.
    /// </summary>
    /// <remarks>
    /// Se calculan en su empresa operativa. El administrador de la empresa u operador global recibe
    /// el comodín "*". Sin empresa operativa, con membresía inactiva o sin perfil asignado devuelve
    /// una lista vacía.
    /// </remarks>
    /// <response code="200">Permisos efectivos del usuario (posiblemente vacíos).</response>
    [HttpGet("me/permissions")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<MyPermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPermissions(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyPermissionsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Cambia la contraseña del usuario autenticado.
    /// </summary>
    /// <remarks>
    /// Único endpoint de cambio de contraseña sobre sí mismo: el usuario sale de la sesión, nunca
    /// del cuerpo. Exige la contraseña actual y una nueva que cumpla la política de complejidad. Al
    /// cambiarla se quita la marca de cambio obligatorio y se revocan todos sus tokens de refresco.
    /// </remarks>
    /// <param name="request">Contraseña actual y contraseña nueva.</param>
    /// <response code="200">Contraseña cambiada (data = true).</response>
    /// <response code="400">El usuario no existe o la contraseña actual no es correcta.</response>
    /// <response code="422">
    /// Falta algún dato o la contraseña nueva no cumple la política de complejidad.
    /// </response>
    [HttpPost("me/change-password")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeMyPassword(
        [FromBody] ChangeMyPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ChangeMyPasswordCommand(request.CurrentPassword, request.NewPassword),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}

/// <summary>Body de POST me/change-password — UserId nunca viaja aquí, se resuelve del contexto autenticado.</summary>
public sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);
