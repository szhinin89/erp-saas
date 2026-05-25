using ERP.API.Contracts;
using ERP.API.Contracts.Subscribers;
using ERP.API.Extensions;
using ERP.Application.Subscribers.DTOs;
using ERP.Application.Subscribers.UseCases.UpdatePasswordResetMode;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SubscriberPublicController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISubscriberRepository _subscriberRepository;

    public SubscriberPublicController(IMediator mediator, ISubscriberRepository subscriberRepository)
    {
        _mediator = mediator;
        _subscriberRepository = subscriberRepository;
    }

    [HttpGet("{id:guid}/public-settings")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SubscriberPublicSettingsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicSettings([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _subscriberRepository.GetByIdAsync(id, ct);
        if (tenant is null || !tenant.IsActive)
            return this.ApiNotFound("Empresa no encontrada.");

        return this.ApiOk(new SubscriberPublicSettingsDto(tenant.Id, (int)tenant.PasswordResetMode));
    }

    [HttpPatch("{id:guid}/password-reset-mode")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
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
}
