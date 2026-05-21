using ERP.API.Extensions;
using ERP.API.Contracts;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.SuperAdminSubscribers;
using ERP.Application.Navigation;
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
    private readonly ISubscriberMenuAdminService _tenantMenuAdmin;

    public PlatformSubscribersController(IMediator mediator, ISubscriberMenuAdminService tenantMenuAdmin)
    {
        _mediator = mediator;
        _tenantMenuAdmin = tenantMenuAdmin;
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
        var r = await _tenantMenuAdmin.GetResolvedMenuForTenantAsync(subscriberId, ct);
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
        var r = await _tenantMenuAdmin.UpsertSubscriberCustomMenuAsync(subscriberId, body.MenuConfigJson, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Elimina menú personalizado; vuelve al menú del plan o global.</summary>
    [HttpDelete("{subscriberId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMenu(Guid subscriberId, CancellationToken ct)
    {
        var r = await _tenantMenuAdmin.DeleteSubscriberCustomMenuAsync(subscriberId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Restablecido")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
