using ERP.API.Extensions;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Consulta del historial. Las operaciones se realizan exclusivamente desde Nota de crédito.</summary>
[ApiController]
[Route("api/v1/purchases/returns")]
[Authorize(Policy = $"perm:{PurchasePermissions.View}")]
[Produces("application/json")]
public sealed class PurchaseReturnController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    /// <summary>Obtiene una devolución de compra por Id, con sus líneas.</summary>
    /// <response code="200">Devolución encontrada.</response>
    /// <response code="404">La devolución no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]


    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPurchaseReturnByIdQuery(id), ct));

    /// <summary>Lista devoluciones de compra paginadas, con filtro opcional por estado.</summary>
    /// <response code="200">Listado paginado.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]

    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPurchaseReturnListQuery(status, page, pageSize), ct),
            "OK"
        );

    /// <summary>
    /// Líneas de una factura de compra confirmada candidatas a devolución, con el remanente ya
    /// calculado (cantidad original − cantidades ya devueltas en devoluciones <c>Authorized</c>
    /// previas) y la bodega original congelada — nunca seleccionable por el cliente.
    /// </summary>
    /// <response code="200">Líneas devolvibles de la factura.</response>
    /// <response code="404">La factura no existe.</response>
    [HttpGet("/api/v1/purchases/invoices/{invoiceId:guid}/returnable-lines")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]


    public async Task<IActionResult> GetReturnableLines(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetReturnableLinesByPurchaseInvoiceQuery(invoiceId), ct)
        );

}
