using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.PlatformSubscribers;
using ERP.Application.Subscribers.DTOs;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

[ApiController]
[Route("api/platform/subscribers")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
public sealed class PlatformSubscribersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISubscriberEntitlementsService _entitlements;
    private readonly ISubscriberRepository _subscribers;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly IAccessRepository _access;

    public PlatformSubscribersController(
        IMediator mediator,
        ISubscriberEntitlementsService entitlements,
        ISubscriberRepository subscribers,
        ISessionModulesResolver sessionModules,
        IAccessRepository access)
    {
        _mediator = mediator;
        _entitlements = entitlements;
        _subscribers = subscribers;
        _sessionModules = sessionModules;
        _access = access;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlatformSubscribersQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<PlatformSubscriberItemDto>());
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SessionResponseDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] PlatformCreateSubscriberWithAdminCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpGet("{subscriberId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid subscriberId, CancellationToken ct)
    {
        var tenant = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (tenant is null)
            return this.ApiNotFound("Suscriptor no encontrado.");

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(subscriberId, ct);
        return this.ApiOk(SubscriberDto.FromSubscriber(tenant, modules));
    }

    [HttpGet("{subscriberId:guid}/users")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSubscriberUsers(Guid subscriberId, CancellationToken ct)
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

    [HttpGet("{subscriberId:guid}/entitlements")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberEntitlementsSnapshot>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntitlements(Guid subscriberId, CancellationToken ct)
    {
        var snapshot = await _entitlements.GetEntitlementsSnapshotAsync(subscriberId, ct);
        return this.ApiOk(snapshot);
    }
}
