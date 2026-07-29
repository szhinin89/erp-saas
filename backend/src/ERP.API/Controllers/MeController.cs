using ERP.API.Attributes;
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
[AppFeature("Me API", "perm:session.me.api", "🧩", null, null, 987, IsVisibleInMenu = false)]
[Route("api/v1/me")]
[Produces("application/json")]
public sealed class MeController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Menú lateral filtrado por permisos del usuario autenticado.
    /// El resultado ya está filtrado — el frontend solo renderiza, no evalúa permisos.
    /// </summary>
    [HttpGet("menu")]
    [Authorize(Policy = "Session")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<NavMenuGroupDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetMenu(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSessionMenuQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<NavMenuGroupDto>());
    }
}
