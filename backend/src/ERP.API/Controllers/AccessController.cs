using ERP.API.Contracts;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.BootstrapLogin;
using ERP.Application.Access.UseCases.SwitchTenant;
using ERP.Application.Access.UseCases.RegisterTenantWithAdmin;
using ERP.Application.Access.UseCases.UpsertMembership;
using ERP.Application.Access.UseCases.RevokeMembership;
using ERP.Application.Access.UseCases.Profiles;
using ERP.Application.Access.UseCases.TenantAccess;
using ERP.Application.Access.UseCases.SuperAdminTenants;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Navigation.UseCases.GetSessionMenu;
using ERP.Application.Common;
using ERP.Domain.Tenants.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Identity &amp; Access Management (IAM).
/// Implementa autenticación en 2 pasos: Bootstrap (sin acceso a negocio) → Session (con tenant).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AccessController : ControllerBase
{
    private readonly BootstrapLoginHandler _bootstrapLoginHandler;
    private readonly SwitchTenantHandler _switchTenantHandler;
    private readonly RegisterTenantWithAdminHandler _registerTenantWithAdminHandler;
    private readonly UpsertMembershipHandler _upsertMembershipHandler;
    private readonly RevokeMembershipHandler _revokeMembershipHandler;
    private readonly GetProfilesHandler _getProfilesHandler;
    private readonly CreateProfileHandler _createProfileHandler;
    private readonly UpdateProfileHandler _updateProfileHandler;
    private readonly GetTenantMembershipsHandler _getTenantMembershipsHandler;
    private readonly TenantUpsertMembershipHandler _tenantUpsertMembershipHandler;
    private readonly TenantRevokeMembershipHandler _tenantRevokeMembershipHandler;
    private readonly ITenantRepository _tenantRepository;
    private readonly SuperAdminCreateTenantWithAdminHandler _superAdminCreateTenantWithAdminHandler;
    private readonly GetMyPermissionsHandler _getMyPermissionsHandler;
    private readonly UpsertProfilePermissionsHandler _upsertProfilePermissionsHandler;
    private readonly GetProfilePermissionsHandler _getProfilePermissionsHandler;
    private readonly GetSessionMenuHandler _getSessionMenuHandler;

    public AccessController(
        BootstrapLoginHandler bootstrapLoginHandler,
        SwitchTenantHandler switchTenantHandler,
        RegisterTenantWithAdminHandler registerTenantWithAdminHandler,
        UpsertMembershipHandler upsertMembershipHandler,
        RevokeMembershipHandler revokeMembershipHandler,
        GetProfilesHandler getProfilesHandler,
        CreateProfileHandler createProfileHandler,
        UpdateProfileHandler updateProfileHandler,
        GetTenantMembershipsHandler getTenantMembershipsHandler,
        TenantUpsertMembershipHandler tenantUpsertMembershipHandler,
        TenantRevokeMembershipHandler tenantRevokeMembershipHandler,
        ITenantRepository tenantRepository,
        SuperAdminCreateTenantWithAdminHandler superAdminCreateTenantWithAdminHandler,
        GetMyPermissionsHandler getMyPermissionsHandler,
        UpsertProfilePermissionsHandler upsertProfilePermissionsHandler,
        GetProfilePermissionsHandler getProfilePermissionsHandler,
        GetSessionMenuHandler getSessionMenuHandler)
    {
        _bootstrapLoginHandler = bootstrapLoginHandler;
        _switchTenantHandler = switchTenantHandler;
        _registerTenantWithAdminHandler = registerTenantWithAdminHandler;
        _upsertMembershipHandler = upsertMembershipHandler;
        _revokeMembershipHandler = revokeMembershipHandler;
        _getProfilesHandler = getProfilesHandler;
        _createProfileHandler = createProfileHandler;
        _updateProfileHandler = updateProfileHandler;
        _getTenantMembershipsHandler = getTenantMembershipsHandler;
        _tenantUpsertMembershipHandler = tenantUpsertMembershipHandler;
        _tenantRevokeMembershipHandler = tenantRevokeMembershipHandler;
        _tenantRepository = tenantRepository;
        _superAdminCreateTenantWithAdminHandler = superAdminCreateTenantWithAdminHandler;
        _getMyPermissionsHandler = getMyPermissionsHandler;
        _upsertProfilePermissionsHandler = upsertProfilePermissionsHandler;
        _getProfilePermissionsHandler = getProfilePermissionsHandler;
        _getSessionMenuHandler = getSessionMenuHandler;
    }

    /// <summary>Login (paso 1): retorna bootstrap token + empresas accesibles.</summary>
    /// <remarks>
    /// El bootstrap token es de corta duración y NO permite acceder a endpoints de negocio.
    /// Solo se usa para ejecutar el paso 2 (`switch-tenant`).
    /// </remarks>
    /// <response code="200">Bootstrap token y lista de empresas.</response>
    /// <response code="401">Credenciales inválidas o usuario inactivo.</response>
    [HttpPost("bootstrap-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BootstrapLoginResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BootstrapLogin([FromBody] BootstrapLoginCommand command, CancellationToken ct)
    {
        var result = await _bootstrapLoginHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<BootstrapLoginResponseDto?>(true, "OK", result.Value))
            : Unauthorized(new ApiResponse<object>(false, result.Error ?? "Unauthorized", new { }));
    }

    /// <summary>Switch tenant (paso 2): emite session token para la empresa seleccionada.</summary>
    /// <remarks>
    /// Requiere bootstrap token (Authorization: Bearer).
    /// Valida membresía activa y retorna un token de sesión con `tenant_id` y `role`.
    /// </remarks>
    /// <response code="200">Session token listo para usar en el ERP.</response>
    /// <response code="400">No tiene acceso al tenant seleccionado.</response>
    /// <response code="401">Bootstrap token ausente o inválido.</response>
    [HttpPost("switch-tenant")]
    [Authorize(Policy = "Bootstrap")]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantCommand command, CancellationToken ct)
    {
        var result = await _switchTenantHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<SessionResponseDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>Registro de empresa + usuario administrador (onboarding).</summary>
    /// <remarks>
    /// Crea el tenant, crea el usuario global (email único en el sistema) y le asigna una membresía Admin
    /// solo en esa empresa.
    /// </remarks>
    /// <response code="201">Tenant creado + session token del admin.</response>
    /// <response code="400">Slug duplicado o email ya registrado.</response>
    [HttpPost("register-tenant")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantWithAdminCommand command, CancellationToken ct)
    {
        var result = await _registerTenantWithAdminHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<SessionResponseDto?>(true, "Creado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>Otorga o actualiza acceso (membership) de un usuario a una empresa.</summary>
    /// <remarks>Solo SuperAdmin puede gestionar membresías cruzadas.</remarks>
    /// <response code="200">Membership creada/actualizada.</response>
    /// <response code="400">Usuario no existe o payload inválido.</response>
    /// <response code="401">Token ausente o inválido.</response>
    /// <response code="403">No es SuperAdmin.</response>
    [HttpPost("memberships/grant")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GrantMembership([FromBody] UpsertMembershipCommand command, CancellationToken ct)
    {
        var result = await _upsertMembershipHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<object>(true, "OK", new { }))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>Revoca acceso (desactiva membership) de un usuario a una empresa.</summary>
    /// <remarks>Solo SuperAdmin puede revocar membresías cruzadas.</remarks>
    /// <response code="200">Membership revocada (idempotente).</response>
    /// <response code="400">Usuario no existe o payload inválido.</response>
    /// <response code="401">Token ausente o inválido.</response>
    /// <response code="403">No es SuperAdmin.</response>
    [HttpPost("memberships/revoke")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeMembership([FromBody] RevokeMembershipCommand command, CancellationToken ct)
    {
        var result = await _revokeMembershipHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<object>(true, "OK", new { }))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    // ── SuperAdmin: empresas ────────────────────────────────────────

    /// <summary>SuperAdmin: lista empresas activas para administración.</summary>
    [HttpGet("superadmin/tenants")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuperAdminTenants(CancellationToken ct)
    {
        var tenants = await _tenantRepository.GetAllAsync(ct);
        var items = tenants
            .Where(t => t.IsActive)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                planCode = t.PlanCode,
                enabledModules = TenantSubscriptionCatalog.GetEffectiveEnabledModules(t),
                hasModuleRestrictions = !string.IsNullOrWhiteSpace(t.EnabledModulesJson),
            })
            .ToList();
        return Ok(new ApiResponse<object>(true, "OK", new { tenants = items }));
    }

    /// <summary>SuperAdmin: crea empresa + Admin inicial (solo para esa empresa).</summary>
    [HttpPost("superadmin/tenants")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SuperAdminCreateTenant([FromBody] SuperAdminCreateTenantWithAdminCommand command, CancellationToken ct)
    {
        var result = await _superAdminCreateTenantWithAdminHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<SessionResponseDto?>(true, "Creado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    // ── Admin del tenant: accesos ───────────────────────────────────

    /// <summary>Admin: lista accesos (memberships) del tenant actual.</summary>
    [HttpGet("tenant/memberships")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantMembershipItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantMemberships([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _getTenantMembershipsHandler.HandleAsync(onlyActive, ct);
        return Ok(new ApiResponse<IReadOnlyList<TenantMembershipItemDto>>(true, "OK", result.Value ?? Array.Empty<TenantMembershipItemDto>()));
    }

    /// <summary>Admin: crea/actualiza acceso de un usuario a este tenant.</summary>
    [HttpPost("tenant/memberships")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertTenantMembership([FromBody] TenantUpsertMembershipCommand command, CancellationToken ct)
    {
        var result = await _tenantUpsertMembershipHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<object>(true, "OK", new { }))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>Admin: revoca acceso (desactiva membership) de un usuario en este tenant.</summary>
    [HttpPost("tenant/memberships/revoke")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeTenantMembership([FromBody] TenantRevokeMembershipCommand command, CancellationToken ct)
    {
        var result = await _tenantRevokeMembershipHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<object>(true, "OK", new { }))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    // ── Perfiles (por tenant) ───────────────────────────────────────

    /// <summary>Lista perfiles de acceso del tenant actual.</summary>
    [HttpGet("profiles")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfiles([FromQuery] bool onlyActive = true, CancellationToken ct = default)
    {
        var result = await _getProfilesHandler.HandleAsync(onlyActive, ct);
        return Ok(new ApiResponse<IReadOnlyList<ProfileDto>>(true, "OK", result.Value ?? Array.Empty<ProfileDto>()));
    }

    /// <summary>Crea un perfil de acceso en el tenant actual.</summary>
    [HttpPost("profiles")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileCommand command, CancellationToken ct)
    {
        var result = await _createProfileHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<ProfileDto?>(true, "Creado", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>Actualiza un perfil de acceso.</summary>
    [HttpPut("profiles/{profileId:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromRoute] Guid profileId, [FromBody] UpdateProfileCommand command, CancellationToken ct)
    {
        if (profileId != command.ProfileId)
            return BadRequest(new ApiResponse<object>(false, "ProfileId no coincide con la ruta.", new { }));

        var result = await _updateProfileHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<ProfileDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    // ── Permisos (por perfil) ───────────────────────────────────────

    /// <summary>Definición del menú lateral (grupos e ítems) desde base de datos; el front aplica i18n y filtros por rol/módulo/permiso.</summary>
    [HttpGet("me/menu")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SessionMenuGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessionMenu(CancellationToken ct)
    {
        var result = await _getSessionMenuHandler.HandleAsync(ct);
        return Ok(new ApiResponse<IReadOnlyList<SessionMenuGroupDto>>(
            result.IsSuccess,
            result.IsSuccess ? "OK" : (result.Error ?? "Error"),
            result.Value ?? Array.Empty<SessionMenuGroupDto>()));
    }

    /// <summary>Retorna los permisos efectivos del usuario en el tenant actual.</summary>
    [HttpGet("me/permissions")]
    [Authorize] // DefaultPolicy = Session
    [ProducesResponseType(typeof(ApiResponse<MyPermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPermissions(CancellationToken ct)
    {
        var result = await _getMyPermissionsHandler.HandleAsync(ct);
        return Ok(new ApiResponse<MyPermissionsDto?>(result.IsSuccess, result.IsSuccess ? "OK" : (result.Error ?? "Error"), result.Value));
    }

    /// <summary>Admin: asigna/revoca permisos a un perfil del tenant.</summary>
    [HttpPut("profiles/{profileId:guid}/permissions")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertProfilePermissions([FromRoute] Guid profileId, [FromBody] UpsertProfilePermissionsCommand command, CancellationToken ct)
    {
        if (profileId != command.ProfileId)
            return BadRequest(new ApiResponse<object>(false, "ProfileId no coincide con la ruta.", new { }));

        var result = await _upsertProfilePermissionsHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<object>(true, "OK", new { }))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>Admin: lee permisos actuales de un perfil del tenant.</summary>
    [HttpGet("profiles/{profileId:guid}/permissions")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<ProfilePermissionsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfilePermissions([FromRoute] Guid profileId, CancellationToken ct)
    {
        var result = await _getProfilePermissionsHandler.HandleAsync(profileId, ct);
        return Ok(new ApiResponse<ProfilePermissionsDto?>(result.IsSuccess, result.IsSuccess ? "OK" : (result.Error ?? "Error"), result.Value));
    }
}

