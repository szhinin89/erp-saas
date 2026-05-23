using ERP.API.Extensions;
using ERP.API.Contracts;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.SuperAdminSubscribers;
using ERP.Application.Navigation;
using ERP.Application.Platform.Subscribers.UseCases;
using ERP.Application.Subscriptions;
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

    public PlatformSubscribersController(
        IMediator mediator,
        ISubscriberMenuAdminService tenantMenuAdmin,
        ISubscriberEntitlementsService entitlements)
    {
        _mediator = mediator;
        _subscriberMenuAdmin = tenantMenuAdmin;
        _entitlements = entitlements;
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
