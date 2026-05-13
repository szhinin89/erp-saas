using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Navigation.UseCases.GetSessionMenu;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Alias de sesión para el usuario autenticado (<c>/api/me/*</c>).</summary>
[ApiController]
[Route("api/me")]
[Produces("application/json")]
public sealed class MeController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeController(IMediator mediator) => _mediator = mediator;

    /// <summary>Mismo contrato que <c>GET /api/access/me/menu</c> (menú lateral resuelto por tenant/plan).</summary>
    [HttpGet("menu")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SessionMenuGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSessionMenuQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SessionMenuGroupDto>());
    }
}
