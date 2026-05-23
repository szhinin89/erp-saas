using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin.UseCases.SuperAdminGlobal;
using ERP.Application.Platform.Audit;
using ERP.Application.Platform.Users;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Platform.Audit.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>Platform Layer — operadores platform.</summary>
[ApiController]
[Route("api/platform/users")]
[Authorize(Roles = PlatformAuthorizationRoles.Operators)]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformUsersController : ControllerBase
{
    private readonly IPlatformUsersReader _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPlatformAuditReader _audit;
    private readonly IMediator _mediator;

    public PlatformUsersController(
        IPlatformUsersReader users,
        IRefreshTokenRepository refreshTokens,
        IPlatformAuditReader audit,
        IMediator mediator)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PlatformUsersPage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var page = await _users.ListAsync(ct);
        return this.ApiOk(page);
    }

    [HttpGet("{userId:guid}/sessions")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActiveSessions(Guid userId, CancellationToken ct)
    {
        var platformTokens = await _refreshTokens.GetActiveByUserAsync(userId, Guid.Empty, ct);
        return this.ApiOk(new
        {
            sessions = platformTokens.Select(t => new
            {
                t.Id,
                t.CreatedAt,
                t.ExpiresAt,
                t.IsRevoked,
            }),
        });
    }

    [HttpGet("impersonation-history")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImpersonationHistory([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var logs = await _audit.ListRecentAsync(limit, targetSubscriberId: null, ct);
        var filtered = logs
            .Where(x => x.Action == PlatformAuditLog.Actions.SwitchSubscriber)
            .Select(x => new { x.Id, x.Action, x.ActorEmail, x.TargetSubscriberId, x.CreatedAtUtc })
            .ToList();
        return this.ApiOk(new { events = filtered });
    }

    [HttpDelete("{userId:guid}/sessions")]
    [Authorize(Roles = PlatformAuthorizationRoles.Mutators)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeSessions(
        Guid userId,
        [FromQuery] Guid subscriberId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new RevokeSuperAdminUserSessionsCommand(userId, subscriberId), ct);
        return result.IsSuccess
            ? this.ApiOk(result.Value!, "OK")
            : this.ApiBadRequest(result.Error ?? "Error");
    }
}
