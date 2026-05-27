using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Contracts.Subscribers;
using ERP.API.Extensions;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberCommercialProfile;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Interfaces;
using System.Security.Claims;

namespace ERP.API.Controllers;

[ApiController]
[AppFeature("Subscribers API", "perm:subscribers.api", "🧩", null, null, 990, IsVisibleInMenu = false)]
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
        _mediator             = mediator;
        _subscriberRepository = subscriberRepository;
        _sessionModules       = sessionModules;
        _currentSubscriber    = currentSubscriber;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        if (!CanAccessOwnSubscriber(id))
            return Forbid();

        var tenant = await _subscriberRepository.GetByIdAsync(id, ct);
        if (tenant is null)
            return this.ApiNotFound("Empresa no encontrada.");

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(id, ct);
        return this.ApiOk(SubscriberDto.FromSubscriber(tenant, modules));
    }

    [HttpPatch("{id:guid}/company")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCompany(
        [FromRoute] Guid id,
        [FromBody] UpdateSubscriberCompanyRequest body,
        CancellationToken ct)
    {
        if (!CanAccessOwnSubscriber(id))
            return Forbid();

        var command = new UpdateSubscriberCommercialProfileCommand(
            id, body.Name, body.Slug, body.DisplayOrder, body.Priority, body.PreferredLanguage);

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
