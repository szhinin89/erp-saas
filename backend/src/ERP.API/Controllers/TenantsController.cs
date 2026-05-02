using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Tenants.UseCases.CreateTenant;
using ERP.Application.Tenants.UseCases.UpdatePasswordResetMode;
using ERP.Application.Tenants.UseCases.UpdateTenantSubscription;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión de tenants (empresas).
/// Restringido: solo accesible por administradores del sistema.
/// En producción considerar un rol dedicado como "SystemAdmin".
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TenantsController : ControllerBase
{
    private readonly CreateTenantHandler _createHandler;
    private readonly UpdateTenantPasswordResetModeHandler _updatePasswordResetModeHandler;
    private readonly UpdateTenantSubscriptionHandler _updateTenantSubscriptionHandler;
    private readonly ITenantRepository _tenantRepository;

    public TenantsController(
        CreateTenantHandler createHandler,
        UpdateTenantPasswordResetModeHandler updatePasswordResetModeHandler,
        UpdateTenantSubscriptionHandler updateTenantSubscriptionHandler,
        ITenantRepository tenantRepository)
    {
        _createHandler = createHandler;
        _updatePasswordResetModeHandler = updatePasswordResetModeHandler;
        _updateTenantSubscriptionHandler = updateTenantSubscriptionHandler;
        _tenantRepository = tenantRepository;
    }

    /// <summary>Crea un nuevo tenant (empresa) en el sistema.</summary>
    /// <remarks>El slug debe ser único y se usa como identificador amigable del tenant.</remarks>
    /// <response code="201">Tenant creado correctamente.</response>
    /// <response code="400">El slug ya está en uso.</response>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantCommand command,
        CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<TenantDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }

    /// <summary>
    /// Retorna configuración pública mínima del tenant (sin datos sensibles).
    /// Útil para flujos anónimos como recuperación de contraseña.
    /// </summary>
    [HttpGet("{id:guid}/public-settings")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TenantPublicSettingsDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicSettings([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, ct);
        if (tenant is null || !tenant.IsActive)
            return NotFound(new ApiResponse<object>(false, "Empresa no encontrada.", new { }));

        return Ok(new ApiResponse<TenantPublicSettingsDto?>(
            true,
            "OK",
            new TenantPublicSettingsDto(tenant.Id, (int)tenant.PasswordResetMode)));
    }

    /// <summary>
    /// Actualiza el modo de recuperación de contraseña del tenant.
    /// Restringido a Admin.
    /// </summary>
    [HttpPatch("{id:guid}/password-reset-mode")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePasswordResetMode(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantPasswordResetModeCommand command,
        CancellationToken ct)
    {
        if (id != command.TenantId)
            return BadRequest(new ApiResponse<object>(false, "TenantId no coincide con la ruta.", new { }));

        var result = await _updatePasswordResetModeHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<object>(true, "OK", new { }))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }

    /// <summary>
    /// Actualiza plan comercial y módulos contratados del tenant (JSON de módulos habilitados).
    /// Solo SuperAdmin: define qué módulos puede usar la empresa frente a permisos y API.
    /// </summary>
    [HttpPatch("{id:guid}/subscription")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSubscription(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantSubscriptionBody body,
        CancellationToken ct)
    {
        var command = new UpdateTenantSubscriptionCommand(id, body.PlanCode, body.EnabledModules);
        var result = await _updateTenantSubscriptionHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<TenantDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object>(false, result.Error ?? "Error", new { }));
    }
}

/// <summary>Cuerpo para <c>PATCH .../subscription</c> (plan + lista de claves de módulo).</summary>
public sealed class UpdateTenantSubscriptionBody
{
    public string? PlanCode { get; set; }
    public List<string>? EnabledModules { get; set; }
}
