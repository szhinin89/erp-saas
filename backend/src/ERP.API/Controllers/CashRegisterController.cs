using ERP.API.Extensions;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/cash-registers")]
[Authorize]
[Produces("application/json")]
public sealed class CashRegisterController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashRegisterController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetByCurrentBranch(
        [FromQuery] bool? activeOnly,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCashRegistersByCurrentBranchQuery(activeOnly), ct),
            "OK"
        );

    /// <summary>Listado de administración — empresa completa, todas las sucursales.</summary>
    [HttpGet("all")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? activeOnly,
        [FromQuery] string? search,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetAllCashRegistersQuery(activeOnly, search), ct),
            "OK"
        );

    /// <summary>Puntos de emisión activos de una Sucursal — selector cascada del formulario de Caja.</summary>
    [HttpGet("emission-point-lookups")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetEmissionPointLookups(
        [FromQuery] Guid branchId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetEmissionPointLookupsByBranchQuery(branchId), ct),
            "OK"
        );

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashRegisterByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCashRegisterCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCashRegisterCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableCashRegisterCommand(id), ct));

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableCashRegisterCommand(id), ct));
}
