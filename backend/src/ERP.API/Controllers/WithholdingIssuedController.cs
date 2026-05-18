using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.Retenciones;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[AppFeature("Retenciones compras", "perm:purchases.withholding-issued.view", "🔒", "/purchases/withholding-issued", "perm:purchases.invoices.view", 48)]
[ApiController]
[Route("api/purchases/withholding-issued")]
[Authorize]
[Produces("application/json")]
public sealed class WithholdingIssuedController : ControllerBase
{
    private readonly IMediator _mediator;

    public WithholdingIssuedController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:purchases.withholding-issued.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<IssuedRetentionListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? proveedorId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPurchaseIssuedRetentionsListQuery(proveedorId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<IssuedRetentionListItemDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:purchases.withholding-issued.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Generate([FromBody] GenerateIssuedRetentionCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Generado");
    }

    [HttpPut("{id:guid}/enviar")]
    [Authorize(Policy = "perm:purchases.withholding-issued.send")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SendIssuedRetentionCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviado");
    }
}
