using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Tenants.UseCases.CreateTenant;
using ERP.Application.Tenants.UseCases.UpdateTenantGlobalParameters;
using ERP.Application.Tenants.UseCases.UpdatePasswordResetMode;
using ERP.Application.Tenants.UseCases.UpdateTenantCompany;
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
    private readonly UpdateTenantCompanyHandler _updateTenantCompanyHandler;
    private readonly UpdateTenantGlobalParametersHandler _updateTenantGlobalParametersHandler;
    private readonly ITenantRepository _tenantRepository;

    public TenantsController(
        CreateTenantHandler createHandler,
        UpdateTenantPasswordResetModeHandler updatePasswordResetModeHandler,
        UpdateTenantSubscriptionHandler updateTenantSubscriptionHandler,
        UpdateTenantCompanyHandler updateTenantCompanyHandler,
        UpdateTenantGlobalParametersHandler updateTenantGlobalParametersHandler,
        ITenantRepository tenantRepository)
    {
        _createHandler = createHandler;
        _updatePasswordResetModeHandler = updatePasswordResetModeHandler;
        _updateTenantSubscriptionHandler = updateTenantSubscriptionHandler;
        _updateTenantCompanyHandler = updateTenantCompanyHandler;
        _updateTenantGlobalParametersHandler = updateTenantGlobalParametersHandler;
        _tenantRepository = tenantRepository;
    }

    /// <summary>Obtiene el detalle de un tenant (SuperAdmin). Incluye datos comerciales/legales y suscripción.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, ct);
        if (tenant is null)
            return this.ApiNotFound("Empresa no encontrada.");

        return this.ApiOk(TenantDto.FromTenant(tenant));
    }

    /// <summary>Actualiza datos comerciales/legales de la empresa (SuperAdmin).</summary>
    [HttpPatch("{id:guid}/company")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCompany(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantCompanyRequest body,
        CancellationToken ct)
    {
        var command = new UpdateTenantCompanyCommand(
            id,
            body.Name,
            body.Slug,
            body.Ruc,
            body.ShortName,
            body.TradeName,
            body.Dinardap,
            body.LogoUrl,
            body.DisplayOrder,
            body.Priority);

        var result = await _updateTenantCompanyHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza parámetros globales de la empresa (SuperAdmin), dependientes del plan comercial.</summary>
    [HttpPatch("{id:guid}/global-parameters")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateGlobalParameters(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantGlobalParametersBody body,
        CancellationToken ct)
    {
        var command = new UpdateTenantGlobalParametersCommand(
            id,
            body.ElectronicBillingTrialEnabled);

        var result = await _updateTenantGlobalParametersHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
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
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantCommand command,
        CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
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
            return this.ApiNotFound("Empresa no encontrada.");

        return this.ApiOk(new TenantPublicSettingsDto(tenant.Id, (int)tenant.PasswordResetMode));
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
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePasswordResetMode(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantPasswordResetModeCommand command,
        CancellationToken ct)
    {
        if (id != command.TenantId)
            return this.ApiBadRequest("TenantId no coincide con la ruta.");

        var result = await _updatePasswordResetModeHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? this.ApiOk(new { })
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    /// <summary>
    /// Actualiza el código de plan del tenant y la lista de módulos habilitados (<c>enabledModules</c>).
    /// Solo SuperAdmin: acota qué módulos puede usar la empresa frente a permisos y API.
    /// </summary>
    /// <remarks>
    /// La composición detallada del plan (features por ítem de menú) se administra con
    /// <c>PUT /api/superadmin/saas-plans/{planId}/features</c>; ver <c>docs/COMPANIES-PLAN-MENU-ADMIN.md</c>.
    /// </remarks>
    [HttpPatch("{id:guid}/subscription")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSubscription(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantSubscriptionBody body,
        CancellationToken ct)
    {
        var command = new UpdateTenantSubscriptionCommand(id, body.PlanCode, body.EnabledModules);
        var result = await _updateTenantSubscriptionHandler.HandleAsync(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}

/// <summary>Cuerpo para <c>PATCH .../subscription</c> (plan + lista de claves de módulo).</summary>
public sealed class UpdateTenantSubscriptionBody
{
    public string? PlanCode { get; set; }
    public List<string>? EnabledModules { get; set; }
}

/// <summary>Cuerpo para <c>PATCH .../company</c> (datos de empresa sin admin ni suscripción).</summary>
public sealed class UpdateTenantCompanyRequest
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Ruc { get; set; }
    public string? ShortName { get; set; }
    public string? TradeName { get; set; }
    public string? Dinardap { get; set; }
    public string? LogoUrl { get; set; }
    public int DisplayOrder { get; set; }
    public int Priority { get; set; }
}

/// <summary>Cuerpo para <c>PATCH .../global-parameters</c> (parámetros globales por empresa).</summary>
public sealed class UpdateTenantGlobalParametersBody
{
    public bool ElectronicBillingTrialEnabled { get; set; }
}
