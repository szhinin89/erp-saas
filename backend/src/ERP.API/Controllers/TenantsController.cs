using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Subscribers.UseCases.CreateSubscriber;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberGlobalParameters;
using ERP.Application.Subscribers.UseCases.UpdatePasswordResetMode;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberCompany;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberSubscription;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberOperationalSettings;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Interfaces;
using System.Security.Claims;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión de subscribers (empresas).
/// Restringido: solo accesible por administradores del sistema.
/// </summary>
[ApiController]
[AppFeature("Tenants API", "perm:subscribers.api", "🧩", null, null, 990, IsVisibleInMenu = false)]
[Route("api/[controller]")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public class SubscribersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ICurrentSubscriber _currentSubscriber;

    public SubscribersController(
        IMediator mediator,
        ISubscriberRepository tenantRepository,
        ISessionModulesResolver sessionModules,
        ICurrentSubscriber currentSubscriber)
    {
        _mediator = mediator;
        _tenantRepository = tenantRepository;
        _sessionModules = sessionModules;
        _currentSubscriber = currentSubscriber;
    }

    /// <summary>Obtiene el detalle de un tenant (SuperAdmin o Admin de la misma cuenta).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        if (!CanAccessSubscriber(id))
            return Forbid();

        var tenant = await _tenantRepository.GetByIdAsync(id, ct);
        if (tenant is null)
            return this.ApiNotFound("Empresa no encontrada.");

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(id, ct);
        return this.ApiOk(SubscriberDto.FromTenant(tenant, modules));
    }

    /// <summary>Actualiza datos comerciales/legales de la empresa (SuperAdmin o Admin de la misma cuenta).</summary>
    [HttpPatch("{id:guid}/company")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCompany(
        [FromRoute] Guid id,
        [FromBody] UpdateSubscriberCompanyRequest body,
        CancellationToken ct)
    {
        if (!CanAccessSubscriber(id))
            return Forbid();

        var command = new UpdateSubscriberCompanyCommand(
            id, body.Name, body.Slug, body.Ruc, body.ShortName,
            body.TradeName, body.Dinardap, body.LogoUrl, body.DisplayOrder, body.Priority);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza parámetros globales de la empresa (SuperAdmin).</summary>
    [HttpPatch("{id:guid}/global-parameters")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateGlobalParameters(
        [FromRoute] Guid id,
        [FromBody] UpdateSubscriberGlobalParametersBody body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSubscriberGlobalParametersCommand(id, body.ElectronicBillingTrialEnabled), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Crea un nuevo tenant (empresa) en el sistema.</summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateSubscriberCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Retorna configuración pública mínima del tenant (sin datos sensibles).</summary>
    [HttpGet("{id:guid}/public-settings")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SubscriberPublicSettingsDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicSettings([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, ct);
        if (tenant is null || !tenant.IsActive)
            return this.ApiNotFound("Empresa no encontrada.");

        return this.ApiOk(new SubscriberPublicSettingsDto(tenant.Id, (int)tenant.PasswordResetMode));
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
        [FromBody] UpdateSubscriberPasswordResetModeCommand command,
        CancellationToken ct)
    {
        if (id != command.SubscriberId)
            return this.ApiBadRequest("SubscriberId no coincide con la ruta.");

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
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateOperationalSettings(
        [FromRoute] Guid id,
        [FromBody] UpdateSubscriberOperationalSettingsRequest body,
        CancellationToken ct)
    {
        var command = new UpdateSubscriberOperationalSettingsCommand(
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
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSubscription(
        [FromRoute] Guid id,
        [FromBody] UpdateSubscriberSubscriptionBody body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSubscriberSubscriptionCommand(id, body.PlanCode, body.EnabledModules), ct);
        return this.ToOkOrBadRequest(result);
    }

    private bool CanAccessSubscriber(Guid subscriberId)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return _currentSubscriber.SubscriberId == subscriberId;

        return false;
    }
}

public sealed class UpdateSubscriberSubscriptionBody
{
    public string? PlanCode { get; set; }
    public List<string>? EnabledModules { get; set; }
}

public sealed class UpdateSubscriberCompanyRequest
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

public sealed class UpdateSubscriberGlobalParametersBody
{
    public bool ElectronicBillingTrialEnabled { get; set; }
}

public sealed class UpdateSubscriberOperationalSettingsRequest
{
    public string Currency          { get; set; } = "USD";
    public string Language          { get; set; } = "es";
    public string Timezone          { get; set; } = "America/Guayaquil";
    public string? InvoicePrefix    { get; set; }
    public int DefaultCreditDays    { get; set; } = 30;
}
