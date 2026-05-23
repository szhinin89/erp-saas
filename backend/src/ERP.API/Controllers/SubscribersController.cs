using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Subscribers.UseCases.UpdatePasswordResetMode;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberSaasProfile;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberOperationalSettings;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Interfaces;
using System.Security.Claims;

namespace ERP.API.Controllers;

/// <summary>
/// ERP Runtime — tenant Admin y endpoints públicos del suscriptor.
/// Control plane SaaS (operador platform): exclusivamente <c>/api/platform/subscribers</c>.
/// </summary>
[ApiController]
[AppFeature("Tenants API", "perm:subscribers.api", "🧩", null, null, 990, IsVisibleInMenu = false)]
[Route("api/[controller]")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public class SubscribersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ICurrentSubscriber _currentSubscriber;

    public SubscribersController(
        IMediator mediator,
        ISubscriberRepository subscriberRepository,
        ISessionModulesResolver sessionModules,
        ICurrentSubscriber currentSubscriber)
    {
        _mediator = mediator;
        _subscriberRepository = subscriberRepository;
        _sessionModules = sessionModules;
        _currentSubscriber = currentSubscriber;
    }

    /// <summary>Obtiene el detalle del suscriptor (Admin de la propia cuenta).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        if (!CanAccessOwnSubscriber(id))
            return Forbid();

        var tenant = await _subscriberRepository.GetByIdAsync(id, ct);
        if (tenant is null)
            return this.ApiNotFound("Empresa no encontrada.");

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(id, ct);
        return this.ApiOk(SubscriberDto.FromTenant(tenant, modules));
    }

    /// <summary>Actualiza datos comerciales/legales (Admin de la propia cuenta).</summary>
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
        if (!CanAccessOwnSubscriber(id))
            return Forbid();

        var command = new UpdateSubscriberSaasProfileCommand(
            id, body.Name, body.Slug, body.Ruc, body.ShortName,
            body.TradeName, body.Dinardap, body.LogoUrl, body.DisplayOrder, body.Priority);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Retorna configuración pública mínima del tenant (sin datos sensibles).</summary>
    [HttpGet("{id:guid}/public-settings")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SubscriberPublicSettingsDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicSettings([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _subscriberRepository.GetByIdAsync(id, ct);
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
    /// Actualiza parámetros operativos: moneda, idioma, zona horaria, prefijo de factura y días de crédito.
    /// Accesible por el Admin de la propia cuenta.
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
        if (!CanAccessOwnSubscriber(id))
            return Forbid();

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

    private bool CanAccessOwnSubscriber(Guid subscriberId)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return false;

        return _currentSubscriber.SubscriberId == subscriberId;
    }
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

public sealed class UpdateSubscriberOperationalSettingsRequest
{
    public string Currency          { get; set; } = "USD";
    public string Language          { get; set; } = "es";
    public string Timezone          { get; set; } = "America/Guayaquil";
    public string? InvoicePrefix    { get; set; }
    public int DefaultCreditDays    { get; set; } = 30;
}
