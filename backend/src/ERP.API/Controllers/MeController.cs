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
    /// Devuelve el menú lateral filtrado por los permisos del usuario.
    /// </summary>
    /// <remarks>
    /// El resultado ya viene filtrado: el frontend solo lo renderiza y no evalúa permisos. Si el usuario no tiene
    /// opciones, la lista va vacía.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Grupos de menú del usuario.</response>
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
