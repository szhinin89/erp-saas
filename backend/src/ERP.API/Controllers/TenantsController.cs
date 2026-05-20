using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Tenants.UseCases.CreateTenant;
using ERP.Application.Tenants.UseCases.UpdateTenantGlobalParameters;
using ERP.Application.Tenants.UseCases.UpdatePasswordResetMode;
using ERP.Application.Tenants.UseCases.UpdateTenantCompany;
using ERP.Application.Tenants.UseCases.UpdateTenantSubscription;
using ERP.Application.Tenants.UseCases.UpdateTenantOperationalSettings;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión de tenants (empresas).
/// Restringido: solo accesible por administradores del sistema.
/// </summary>
[ApiController]
[AppFeature("Tenants API", "perm:tenants.api", "🧩", null, null, 990, IsVisibleInMenu = false)]
[Route("api/[controller]")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public class TenantsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantEntitlementsService _entitlements;

    public TenantsController(
        IMediator mediator,
        ITenantRepository tenantRepository,
        ITenantEntitlementsService entitlements)
    {
        _mediator = mediator;
        _tenantRepository = tenantRepository;
        _entitlements = entitlements;
    }

    /// <summary>Obtiene el detalle de un tenant (SuperAdmin).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, ct);
        if (tenant is null)
            return this.ApiNotFound("Empresa no encontrada.");

        var modules = await TenantSubscriptionCatalog.ResolveEnabledModulesAsync(id, _entitlements, ct);
        return this.ApiOk(TenantDto.FromTenant(tenant, modules));
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
            id, body.Name, body.Slug, body.Ruc, body.ShortName,
            body.TradeName, body.Dinardap, body.LogoUrl, body.DisplayOrder, body.Priority);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza parámetros globales de la empresa (SuperAdmin).</summary>
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
        var result = await _mediator.Send(new UpdateTenantGlobalParametersCommand(id, body.ElectronicBillingTrialEnabled), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Crea un nuevo tenant (empresa) en el sistema.</summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Retorna configuración pública mínima del tenant (sin datos sensibles).</summary>
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

    /// <summary>Actualiza el modo de recuperación de contraseña del tenant.</summary>
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

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess
            ? this.ApiOk(new { })
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    /// <summary>
    /// Actualiza los parámetros operativos de la empresa: moneda, idioma, zona horaria,
    /// prefijo de factura y días de crédito por defecto.
    /// Accesible por el administrador de la propia empresa o por SuperAdmin.
    /// </summary>
    [HttpPatch("{id:guid}/operational-settings")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<TenantDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateOperationalSettings(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantOperationalSettingsRequest body,
        CancellationToken ct)
    {
        var command = new UpdateTenantOperationalSettingsCommand(
            id,
            body.Currency,
            body.Language,
            body.Timezone,
            body.InvoicePrefix,
            body.DefaultCreditDays);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza el código de plan del tenant y los módulos habilitados.</summary>
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
        var result = await _mediator.Send(new UpdateTenantSubscriptionCommand(id, body.PlanCode, body.EnabledModules), ct);
        return this.ToOkOrBadRequest(result);
    }
}

public sealed class UpdateTenantSubscriptionBody
{
    public string? PlanCode { get; set; }
    public List<string>? EnabledModules { get; set; }
}

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

public sealed class UpdateTenantGlobalParametersBody
{
    public bool ElectronicBillingTrialEnabled { get; set; }
}

public sealed class UpdateTenantOperationalSettingsRequest
{
    public string Currency          { get; set; } = "USD";
    public string Language          { get; set; } = "es";
    public string Timezone          { get; set; } = "America/Guayaquil";
    public string? InvoicePrefix    { get; set; }
    public int DefaultCreditDays    { get; set; } = 30;
}
