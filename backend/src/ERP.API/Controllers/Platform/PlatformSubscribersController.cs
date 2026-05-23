using ERP.API.Extensions;
using ERP.API.Contracts;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.SuperAdminSubscribers;
using ERP.Application.Navigation;
using ERP.Application.Platform.Subscribers.UseCases;
using ERP.Application.Subscribers.DTOs;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberGlobalParameters;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberOperationalSettings;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberSaasProfile;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>
/// Platform Layer — onboarding SaaS, suscriptores, menús por plan/subscriber.
/// No incluye IAM (auth/memberships) ni runtime ERP operativo.
/// </summary>
[ApiController]
[Route("api/platform/subscribers")]
[Authorize(Roles = "SuperAdmin")]
[Tags("Platform")]
public sealed class PlatformSubscribersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISubscriberMenuAdminService _subscriberMenuAdmin;
    private readonly ISubscriberEntitlementsService _entitlements;
    private readonly ISubscriberRepository _subscribers;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly IAccessRepository _access;

    public PlatformSubscribersController(
        IMediator mediator,
        ISubscriberMenuAdminService tenantMenuAdmin,
        ISubscriberEntitlementsService entitlements,
        ISubscriberRepository subscribers,
        ISessionModulesResolver sessionModules,
        IAccessRepository access)
    {
        _mediator = mediator;
        _subscriberMenuAdmin = tenantMenuAdmin;
        _entitlements = entitlements;
        _subscribers = subscribers;
        _sessionModules = sessionModules;
        _access = access;
    }

    /// <summary>Lista suscriptores SaaS para administración de plataforma.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminSubscribersQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SuperAdminSubscriberItemDto>());
    }

    /// <summary>Crea suscriptor + billing + empresa default + admin inicial (orquestación transaccional).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] SuperAdminCreateSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Detalle del suscriptor para el control plane (perfil SaaS + módulos).</summary>
    [HttpGet("{subscriberId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid subscriberId, CancellationToken ct)
    {
        var tenant = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (tenant is null)
            return this.ApiNotFound("Suscriptor no encontrado.");

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(subscriberId, ct);
        return this.ApiOk(SubscriberDto.FromTenant(tenant, modules));
    }

    public sealed record UpdatePlatformSubscriberCompanyBody(
        string Name,
        string Slug,
        string? Ruc,
        string? ShortName,
        string? TradeName,
        string? Dinardap,
        string? LogoUrl,
        int DisplayOrder,
        int Priority);

    /// <summary>Actualiza datos comerciales/legales del suscriptor.</summary>
    [HttpPatch("{subscriberId:guid}/company")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCompany(Guid subscriberId, [FromBody] UpdatePlatformSubscriberCompanyBody body, CancellationToken ct)
    {
        var command = new UpdateSubscriberSaasProfileCommand(
            subscriberId, body.Name, body.Slug, body.Ruc, body.ShortName,
            body.TradeName, body.Dinardap, body.LogoUrl, body.DisplayOrder, body.Priority);
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    public sealed record UpdatePlatformGlobalParametersBody(bool ElectronicBillingTrialEnabled);

    [HttpPatch("{subscriberId:guid}/global-parameters")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGlobalParameters(Guid subscriberId, [FromBody] UpdatePlatformGlobalParametersBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSubscriberGlobalParametersCommand(subscriberId, body.ElectronicBillingTrialEnabled), ct);
        return this.ToOkOrBadRequest(result);
    }

    public sealed record UpdatePlatformOperationalSettingsBody(
        string Currency,
        string Language,
        string Timezone,
        string? InvoicePrefix,
        int DefaultCreditDays);

    [HttpPatch("{subscriberId:guid}/operational-settings")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOperationalSettings(Guid subscriberId, [FromBody] UpdatePlatformOperationalSettingsBody body, CancellationToken ct)
    {
        var command = new UpdateSubscriberOperationalSettingsCommand(
            subscriberId, body.Currency, body.Language, body.Timezone, body.InvoicePrefix, body.DefaultCreditDays);
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>Usuarios identity activos del suscriptor (tenant admins/operators).</summary>
    [HttpGet("{subscriberId:guid}/users")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTenantUsers(Guid subscriberId, CancellationToken ct)
    {
        var users = await _access.GetActiveIdentityUsersForSubscriberAsync(subscriberId, ct);
        return this.ApiOk(new
        {
            users = users.Select(u => new
            {
                u.Id,
                Email = u.Email.Value,
                u.FirstName,
                u.LastName,
                u.IsActive,
                userType = u.UserType.ToString(),
            }),
        });
    }

    public sealed record SubscriberMenuPutBody(string MenuConfigJson);

    /// <summary>Menú efectivo del suscriptor (personalizado, plan o global).</summary>
    [HttpGet("{subscriberId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(Guid subscriberId, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.GetResolvedMenuForTenantAsync(subscriberId, ct);
        if (!r.IsSuccess)
            return this.ApiBadRequest(r.Error ?? "Error");
        var v = r.Value!;
        return this.ApiOk(new
        {
            menu = v.Menu,
            hasCustomMenu = v.HasCustomMenu,
            usedPlanMenu = v.UsedPlanMenu,
            usedGlobalFallback = v.UsedGlobalFallback,
        });
    }

    /// <summary>Guarda menú personalizado por suscriptor.</summary>
    [HttpPut("{subscriberId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutMenu(Guid subscriberId, [FromBody] SubscriberMenuPutBody body, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.UpsertSubscriberCustomMenuAsync(subscriberId, body.MenuConfigJson, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Elimina menú personalizado; vuelve al menú del plan o global.</summary>
    [HttpDelete("{subscriberId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMenu(Guid subscriberId, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.DeleteSubscriberCustomMenuAsync(subscriberId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Restablecido")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    // ── Lifecycle endpoints ─────────────────────────────────────────────────

    public sealed record SubscriberLifecycleNoteBody(string? Notes = null);
    public sealed record SubscriberSuspendBody(string? Reason = null);
    public sealed record SubscriberTrialBody(DateTime TrialEndsAtUtc);
    public sealed record SubscriberGracePeriodBody(DateTime GracePeriodEndsAtUtc, string? Reason = null);
    public sealed record SubscriberChangePlanBody(string NewPlanCode, string? Notes = null);

    /// <summary>Activa un suscriptor suspendido o inactivo.</summary>
    [HttpPatch("{subscriberId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid subscriberId, [FromBody] SubscriberLifecycleNoteBody? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ActivateSubscriberCommand(subscriberId, body?.Notes), ct);
        return this.ToOkOrBadRequest(result, "Activado");
    }

    /// <summary>Suspende un suscriptor (bloquea acceso al ERP).</summary>
    [HttpPatch("{subscriberId:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid subscriberId, [FromBody] SubscriberSuspendBody? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SuspendSubscriberCommand(subscriberId, body?.Reason), ct);
        return this.ToOkOrBadRequest(result, "Suspendido");
    }

    /// <summary>Inicia un período de trial para el suscriptor.</summary>
    [HttpPatch("{subscriberId:guid}/trial")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTrial(Guid subscriberId, [FromBody] SubscriberTrialBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetSubscriberTrialCommand(subscriberId, body.TrialEndsAtUtc), ct);
        return this.ToOkOrBadRequest(result, "Trial iniciado");
    }

    /// <summary>Pone al suscriptor en período de gracia (vence pronto, aún puede operar).</summary>
    [HttpPatch("{subscriberId:guid}/grace-period")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGracePeriod(Guid subscriberId, [FromBody] SubscriberGracePeriodBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetSubscriberGracePeriodCommand(subscriberId, body.GracePeriodEndsAtUtc, body.Reason), ct);
        return this.ToOkOrBadRequest(result, "Período de gracia iniciado");
    }

    /// <summary>Cambia el plan comercial del suscriptor (sincroniza entitlements y caché).</summary>
    [HttpPatch("{subscriberId:guid}/plan")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePlan(Guid subscriberId, [FromBody] SubscriberChangePlanBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangePlatformSubscriberPlanCommand(subscriberId, body.NewPlanCode, body.Notes), ct);
        return this.ToOkOrBadRequest(result, "Plan actualizado");
    }

    /// <summary>Entitlements efectivos del suscriptor (plan + overrides + límites).</summary>
    [HttpGet("{subscriberId:guid}/entitlements")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberEntitlementsSnapshot>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntitlements(Guid subscriberId, CancellationToken ct)
    {
        var snapshot = await _entitlements.GetEntitlementsSnapshotAsync(subscriberId, ct);
        return this.ApiOk(snapshot);
    }
}
