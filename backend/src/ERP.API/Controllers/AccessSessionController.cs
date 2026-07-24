using ERP.API.Authorization;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Access.UseCases.Profiles;
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

    [HttpGet("me/permissions")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<MyPermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPermissions(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyPermissionsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Fase E — único endpoint de cambio de contraseña para un usuario ya autenticado (sobre sí
    /// mismo). Solo exige autenticación (policy "Session"), sin permiso adicional, mismo criterio
    /// que GetMyPermissions.
    /// </summary>
    [HttpPost("me/change-password")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeMyPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ChangeMyPasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

/// <summary>Body de POST me/change-password — UserId nunca viaja aquí, se resuelve del contexto autenticado.</summary>
public sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);
