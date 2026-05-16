using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.EnviarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.AprobarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.CancelarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.VincularFacturaAOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.GetOrdenesCompraList;
using ERP.Application.Modules.Purchasing.UseCases.GetOrdenCompraById;
using ERP.Application.Modules.Purchasing.UseCases.GetOrdenesPendientesPorFacturar;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Ã“rdenes de Compra â€” documento interno que autoriza la compra a un proveedor.
/// Flujo: Borrador â†’ Enviada â†’ Aprobada â‡„ RecibidaParcial â†’ Cerrada (+ Cancelada).
/// </summary>
[AppFeature("Ã“rdenes de compra", "perm:compras.ordenes.view", "ðŸ“", "/compras/ordenes", "perm:compras.facturas.view", 44)]
[ApiController]
[Route("api/compras/ordenes")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseOrdersController(IMediator mediator) => _mediator = mediator;

    // â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Lista paginada de Ã³rdenes de compra con filtros opcionales.</summary>
    /// <remarks>Query params: pageNumber, pageSize, proveedorId, estado, fechaDesde (YYYY-MM-DD), fechaHasta.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:compras.ordenes.view")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrdersPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        int pageNumber = 1, pageSize = 20;
        if (Request.Query.TryGetValue("pageNumber", out var pnv) && int.TryParse(pnv, out var pni)) pageNumber = pni;
        if (Request.Query.TryGetValue("pageSize",   out var psv) && int.TryParse(psv, out var psi)) pageSize   = psi;

        Guid? proveedorId = null;
        if (Request.Query.TryGetValue("proveedorId", out var prov) && Guid.TryParse(prov, out var prid)) proveedorId = prid;

        var status = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;

        DateTime? desde = null, hasta = null;
        if (Request.Query.TryGetValue("fechaDesde", out var fdv) && DateTime.TryParse(fdv, out var fd)) desde = fd;
        if (Request.Query.TryGetValue("fechaHasta", out var fhv) && DateTime.TryParse(fhv, out var fh)) hasta = fh;

        var result = await _mediator.Send(
            new GetPurchaseOrdersListQuery(pageNumber, pageSize, proveedorId, status, desde, hasta), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>
    /// Lista de OC aprobadas o con recepciÃ³n parcial que tienen saldo pendiente por facturar.
    /// Ãštil para el selector al vincular facturas electrÃ³nicas.
    /// </summary>
    [HttpGet("pendientes-por-facturar")]
    [Authorize(Policy = "perm:compras.ordenes.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PurchaseOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingBilling(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrdersPendingBillingQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Retorna el detalle completo de una OC (con lÃ­neas y facturas vinculadas).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:compras.ordenes.view")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPurchaseOrderByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // â”€â”€ Crear â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Crea una OC en estado Borrador.
    /// </summary>
    /// <response code="201">OC creada en estado Borrador.</response>
    /// <response code="400">Datos invÃ¡lidos o proveedor no encontrado.</response>
    [HttpPost]
    [Authorize(Policy = "perm:compras.ordenes.create")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePurchaseOrderCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // â”€â”€ Transiciones de estado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// EnvÃ­a la OC al proveedor (Borrador â†’ Enviada).
    /// </summary>
    [HttpPatch("{id:guid}/enviar")]
    [Authorize(Policy = "perm:compras.ordenes.send")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SendOrderPurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviada");
    }

    /// <summary>
    /// Aprueba la OC (Borrador/Enviada â†’ Aprobada).
    /// Solo una OC aprobada puede recibir facturas vinculadas.
    /// </summary>
    [HttpPatch("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:compras.ordenes.approve")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ApproveOrderPurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Aprobada");
    }

    /// <summary>
    /// Cancela la OC. No posible en Cerrada o ya Cancelada.
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [Authorize(Policy = "perm:compras.ordenes.cancel")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelOrderPurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Cancelada");
    }

    /// <summary>
    /// Vincula una factura electrÃ³nica aprobada a esta OC.
    /// Actualiza las cantidades facturadas y cierra la OC si estÃ¡ completamente cubierta.
    /// </summary>
    /// <response code="200">Factura vinculada. OC actualizada (estado puede cambiar a RecibidaParcial o Cerrada).</response>
    /// <response code="400">Factura no aprobada, ya vinculada, o cantidades excedidas.</response>
    [HttpPost("{id:guid}/vincular-factura")]
    [Authorize(Policy = "perm:compras.ordenes.link-invoice")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkInvoice(
        Guid id,
        [FromBody] LinkInvoiceRequest body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(id, body.PurchaseInvoiceId), ct);
        return this.ToOkOrBadRequest(result, "Factura vinculada");
    }
}

/// <param name="PurchaseInvoiceId">ID de la factura de compra aprobada a vincular.</param>
public sealed record LinkInvoiceRequest(Guid PurchaseInvoiceId);
