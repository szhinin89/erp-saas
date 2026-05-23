using ERP.API.Controllers.Platform;
using System;
using ERP.API.Authorization;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Filters;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.BootstrapLogin;
using ERP.Application.Access.UseCases.SwitchSubscriber;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembership;
using ERP.Application.Access.UseCases.Profiles;
using ERP.Application.Access.UseCases.SubscriberAccess;
using ERP.Application.Access.UseCases.PlatformSubscribers;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Navigation.UseCases.GetSessionMenu;
using ERP.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Identity &amp; Access Management (IAM).
/// Implementa autenticación en 2 pasos: Bootstrap (sin acceso a negocio) → Session (con tenant).
/// </summary>
/// <remarks>
/// Políticas mezcladas a propósito: <c>AllowAnonymous</c> (bootstrap / registro empresa),
/// <c>Bootstrap</c> (<c>switch-subscriber</c>), <c>Session</c> (<c>me/menu</c>, <c>me/permissions</c>),
/// <c>Roles</c> (membresías globales, perfiles, permisos de perfil). Ver criterio P0 en <c>docs/STATUS.md</c> (backlog IAM).
/// </remarks>
[ApiController]
[AppFeature("Access IAM API", "perm:admin.iam.api", "🧩", null, null, 983, IsVisibleInMenu = false)]
[Route("api/admin/iam")]
[Produces("application/json")]
public class AccessController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _authorization;

    public AccessController(
        IMediator mediator,
        IAuthorizationService authorization)
    {
        _mediator = mediator;
        _authorization = authorization;
    }

    /// <summary>Login (paso 1): retorna bootstrap token + empresas accesibles.</summary>
    /// <remarks>
    /// El bootstrap token es de corta duración y NO permite acceder a endpoints de negocio.
    /// Solo se usa para ejecutar el paso 2 (`switch-subscriber`).
    /// </remarks>
    /// <response code="200">Bootstrap token y lista de empresas.</response>
    /// <response code="401">Credenciales inválidas o usuario inactivo.</response>
    [HttpPost("bootstrap-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BootstrapLoginResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BootstrapLogin([FromBody] BootstrapLoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrUnauthorized(result);
    }

    /// <summary>Switch tenant (paso 2): emite session token para la empresa seleccionada.</summary>
    /// <remarks>
    /// Requiere bootstrap token (Authorization: Bearer).
    /// Valida membresía activa y retorna un token de sesión con `subscriber_id` y `role`.
    /// </remarks>
    /// <response code="200">Session token listo para usar en el ERP.</response>
    /// <response code="400">No tiene acceso al tenant seleccionado.</response>
    /// <response code="401">Bootstrap token ausente o inválido.</response>
    [HttpPost("switch-subscriber")]
    [Authorize(Policy = "Bootstrap")]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SwitchSubscriber([FromBody] SwitchSubscriberCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Alias legacy de alta de empresa.
    /// Ruta canónica: <c>POST /api/platform/subscribers</c>.
    /// </summary>
    /// <remarks>
    /// Crea el tenant, crea el usuario global (email único en el sistema) y le asigna una membresía Admin
    /// solo en esa empresa.
    /// </remarks>
    /// <response code="201">Subscriber creado + session token del admin.</response>
    /// <response code="400">Slug duplicado o email ya registrado.</response>
    [HttpPost("register-subscriber")]
    [Obsolete("Legacy IAM route register-tenant. Prefer register-subscriber.")]
    [HttpPost("register-tenant")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterTenant([FromBody] PlatformCreateSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Otorga o actualiza acceso (membership) de un usuario a una empresa.</summary>
    /// <remarks>Solo operador platform puede gestionar membresías cruzadas.</remarks>
    /// <response code="200">CompanyUserMembership creada/actualizada.</response>
    /// <response code="400">Usuario no existe o payload inválido.</response>
    /// <response code="401">Token ausente o inválido.</response>
    /// <response code="403">No es operador platform.</response>
    [HttpPost("company_user_memberships/grant")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GrantCompanyUserMembership([FromBody] UpsertCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    /// <summary>Revoca acceso (desactiva membership) de un usuario a una empresa.</summary>
    /// <remarks>Solo operador platform puede revocar membresías cruzadas.</remarks>
    /// <response code="200">CompanyUserMembership revocada (idempotente).</response>
    /// <response code="400">Usuario no existe o payload inválido.</response>
    /// <response code="401">Token ausente o inválido.</response>
    /// <response code="403">No es operador platform.</response>
    [HttpPost("company_user_memberships/revoke")]
    [Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RevokeCompanyUserMembership([FromBody] RevokeCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    // ── Admin del tenant: accesos ───────────────────────────────────

    /// <summary>Admin: lista accesos (company_user_memberships) del subscriber actual.</summary>
    [HttpGet("subscriber/company_user_memberships")]
    [Obsolete("Legacy IAM route segment 'tenant'. Prefer subscriber/company_user_memberships.")]
    [HttpGet("tenant/company_user_memberships")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriberCompanyUserMemberships([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSubscriberCompanyUserMembershipsQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SubscriberCompanyUserMembershipItemDto>());
    }

    /// <summary>Admin: crea/actualiza acceso de un usuario a este subscriber.</summary>
    [HttpPost("subscriber/company_user_memberships")]
    [Obsolete("Legacy IAM route segment 'tenant'. Prefer subscriber/company_user_memberships.")]
    [HttpPost("tenant/company_user_memberships")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertTenantCompanyUserMembership([FromBody] SubscriberUpsertCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    /// <summary>Admin: revoca acceso (desactiva membership) de un usuario en este subscriber.</summary>
    [HttpPost("subscriber/company_user_memberships/revoke")]
    [Obsolete("Legacy IAM route segment 'tenant'. Prefer subscriber/company_user_memberships/revoke.")]
    [HttpPost("tenant/company_user_memberships/revoke")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.company_user_memberships.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RevokeTenantCompanyUserMembership([FromBody] SubscriberRevokeCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    // ── Perfiles (por tenant) ───────────────────────────────────────

    /// <summary>Lista perfiles de acceso del tenant actual.</summary>
    [HttpGet("profiles")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfiles([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var pfx = PermissionPolicyProvider.Prefix;
        var canProfiles = await _authorization.AuthorizeAsync(User, resource: null, policyName: $"{pfx}access.profiles.view");
        var canMembers = await _authorization.AuthorizeAsync(User, resource: null, policyName: $"{pfx}access.company_user_memberships.view");
        if (!canProfiles.Succeeded && !canMembers.Succeeded)
            return Forbid();

        var result = await _mediator.Send(new GetProfilesQuery(onlyActive), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProfileDto>());
    }

    /// <summary>Crea un perfil de acceso en el tenant actual.</summary>
    [HttpPost("profiles")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Actualiza un perfil de acceso.</summary>
    [HttpPut("profiles/{profileId:guid}")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile([FromRoute] Guid profileId, [FromBody] UpdateProfileCommand command, CancellationToken ct)
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    // ── Permisos (por perfil) ───────────────────────────────────────

    /// <summary>Definición del menú lateral (grupos e ítems) desde base de datos; el front aplica i18n y filtros por rol/módulo/permiso.</summary>
    [HttpGet("me/menu")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SessionMenuGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessionMenu(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSessionMenuQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SessionMenuGroupDto>());
    }

    /// <summary>Retorna los permisos efectivos del usuario en el tenant actual.</summary>
    [HttpGet("me/permissions")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<MyPermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPermissions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyPermissionsQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Admin: asigna/revoca permisos a un perfil del tenant.</summary>
    [HttpPut("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertProfilePermissions([FromRoute] Guid profileId, [FromBody] UpsertProfilePermissionsCommand command, CancellationToken ct)
    {
        if (profileId != command.ProfileId)
            return this.ApiBadRequest("ProfileId no coincide con la ruta.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "OK", () => new { });
    }

    /// <summary>Admin: lee permisos actuales de un perfil del tenant.</summary>
    [HttpGet("profiles/{profileId:guid}/permissions")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = "perm:access.profiles.view")]
    [ProducesResponseType(typeof(ApiResponse<ProfilePermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfilePermissions([FromRoute] Guid profileId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProfilePermissionsQuery(profileId), ct);
        return this.ToOkOrBadRequest(result);
    }
}

