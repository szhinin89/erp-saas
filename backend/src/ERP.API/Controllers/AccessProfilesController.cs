using ERP.API.Authorization;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Access.UseCases.Profiles;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/admin/iam")]
[Produces("application/json")]
public sealed class AccessProfilesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _authorization;

    public AccessProfilesController(IMediator mediator, IAuthorizationService authorization)
    {
        _mediator = mediator;
        _authorization = authorization;
    }

    /// <summary>
    /// Lista los perfiles de acceso de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Por defecto devuelve solo los perfiles activos; con onlyActive=false incluye los inactivos.
    /// Además de la sesión, la operación exige tener al menos uno de los permisos
    /// access.profiles.view o access.company_user_memberships.view; si no tiene ninguno responde
    /// 403 sin cuerpo.
    /// </remarks>
    /// <param name="onlyActive">
    /// true (valor por defecto) devuelve solo perfiles activos; false incluye también los
    /// inactivos.
    /// </param>
    /// <response code="200">
    /// Perfiles de la empresa activa (lista vacía si no hay ninguno).
    /// </response>
    [HttpGet("profiles")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfiles(
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var pfx = PermissionPolicyProvider.Prefix;
        var canProfiles = await _authorization.AuthorizeAsync(
            User,
            resource: null,
            policyName: $"{pfx}access.profiles.view"
        );
        var canMembers = await _authorization.AuthorizeAsync(
            User,
            resource: null,
            policyName: $"{pfx}access.company_user_memberships.view"
        );
        if (!canProfiles.Succeeded && !canMembers.Succeeded)
            return Forbid();

        var result = await _mediator.Send(new GetProfilesQuery(onlyActive), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProfileDto>());
    }

    /// <summary>
    /// Crea un perfil de acceso en la empresa activa.
    /// </summary>
    /// <remarks>
    /// El perfil nace activo y sin permisos; los permisos se asignan después con PUT
    /// profiles/{profileId}/permissions. El nombre es único por empresa (dos empresas pueden
    /// repetirlo). Tenant y empresa salen de la sesión, nunca del cuerpo.
    /// </remarks>
    /// <param name="command">Nombre (obligatorio) y descripción opcional del perfil.</param>
    /// <response code="201">Perfil creado.</response>
    /// <response code="400">El nombre está vacío o solo contiene espacios.</response>
    /// <response code="409">Ya existe un perfil con ese nombre en la empresa.</response>
    [HttpPost("profiles")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProfile(
        [FromBody] CreateProfileCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza nombre, descripción y estado de un perfil de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Reemplaza nombre y descripción y aplica isActive: true activa el perfil y false lo desactiva
    /// (un perfil inactivo deja de conceder permisos efectivos a sus usuarios). Invalida la caché
    /// de permisos de la empresa y la navegación de los usuarios con ese perfil. Un perfil
    /// inexistente o de otra empresa responde 400 (no 404).
    /// </remarks>
    /// <param name="profileId">Id del perfil; debe coincidir con el del cuerpo.</param>
    /// <param name="command">Id del perfil, nombre, descripción y estado activo.</param>
    /// <response code="200">Perfil actualizado.</response>
    /// <response code="400">
    /// El profileId de la ruta no coincide con el del cuerpo, o el perfil no existe en la empresa
    /// activa.
    /// </response>
    /// <response code="409">Ya existe otro perfil con ese nombre en la empresa.</response>
    [HttpPut("profiles/{profileId:guid}")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile(
        [FromRoute] Guid profileId,
        [FromBody] UpdateProfileCommand command,
        CancellationToken cancellationToken
    )
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Guarda de forma atómica los permisos de un perfil.
    /// </summary>
    /// <remarks>
    /// Crea o actualiza cada permiso enviado (isAllowed=true concede, false revoca); los permisos
    /// no enviados no se modifican. El guardado es todo o nada: si algún permiso no existe o no es
    /// asignable no se guarda ninguno.
    ///
    /// Anti-escalamiento: quien no es administrador de la empresa no puede conceder un permiso que
    /// él mismo no tiene efectivo; revocar no pasa por esa comprobación. Invalida la caché de
    /// permisos de la empresa y la navegación de los usuarios del perfil. La lista rejected del
    /// resultado hoy nunca se llena.
    /// </remarks>
    /// <param name="profileId">Id del perfil; debe coincidir con el del cuerpo.</param>
    /// <param name="command">
    /// Id del perfil y lista de permisos (clave de hasta 180 caracteres e isAllowed); al menos uno.
    /// </param>
    /// <response code="200">Permisos guardados; saved lista las claves persistidas.</response>
    /// <response code="400">
    /// El profileId de la ruta no coincide con el del cuerpo, o el perfil no existe en la empresa
    /// activa.
    /// </response>
    /// <response code="403">
    /// No se pudo resolver el contexto operativo de quien asigna, o intenta conceder permisos fuera
    /// de su propio alcance.
    /// </response>
    /// <response code="422">
    /// Permisos inexistentes o no asignables, o cuerpo inválido (lista vacía, clave vacía o de más
    /// de 180 caracteres).
    /// </response>
    [HttpPut("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<PermissionUpsertResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertProfilePermissions(
        [FromRoute] Guid profileId,
        [FromBody] UpsertProfilePermissionsCommand command,
        CancellationToken cancellationToken
    )
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Devuelve los permisos asignados a un perfil.
    /// </summary>
    /// <remarks>
    /// Incluye los permisos concedidos y los revocados (isAllowed), ordenados por clave. Un perfil
    /// inexistente o de otra empresa responde 400 (no 404).
    /// </remarks>
    /// <param name="profileId">Id del perfil de la empresa activa.</param>
    /// <response code="200">Permisos del perfil.</response>
    /// <response code="400">El perfil no existe en la empresa activa.</response>
    [HttpGet("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfilePermissionsDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProfilePermissions(
        [FromRoute] Guid profileId,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new GetProfilePermissionsQuery(profileId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Audita los permisos guardados de un perfil.
    /// </summary>
    /// <remarks>
    /// Devuelve cada permiso asignado con su estado de auditoría, ordenado por clave. Hoy todos se
    /// reportan como Effective: los estados BlockedByPlan y UnknownPrefix existen en el contrato
    /// pero el handler aún no los calcula (deuda registrada en el código).
    /// </remarks>
    /// <param name="profileId">Id del perfil de la empresa activa.</param>
    /// <response code="200">Auditoría de permisos del perfil.</response>
    /// <response code="404">El perfil no existe en la empresa activa.</response>
    [HttpGet("profiles/{profileId:guid}/permission-audit")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<ProfilePermissionAuditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfilePermissionAudit(
        [FromRoute] Guid profileId,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new GetProfilePermissionAuditQuery(profileId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
