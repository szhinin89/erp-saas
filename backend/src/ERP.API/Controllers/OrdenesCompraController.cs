using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;
using ERP.Application.Modules.Compras.UseCases.EnviarOrdenCompra;
using ERP.Application.Modules.Compras.UseCases.AprobarOrdenCompra;
using ERP.Application.Modules.Compras.UseCases.CancelarOrdenCompra;
using ERP.Application.Modules.Compras.UseCases.VincularFacturaAOrdenCompra;
using ERP.Application.Modules.Compras.UseCases.GetOrdenesCompraList;
using ERP.Application.Modules.Compras.UseCases.GetOrdenCompraById;
using ERP.Application.Modules.Compras.UseCases.GetOrdenesPendientesPorFacturar;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Órdenes de Compra — documento interno que autoriza la compra a un proveedor.
/// Flujo: Borrador → Enviada → Aprobada ⇄ RecibidaParcial → Cerrada (+ Cancelada).
/// </summary>
[Modulo("Órdenes de compra", "perm:compras.ordenes.view", "📝", "/compras/ordenes", null, 44)]
[ApiController]
[Route("api/compras/ordenes")]
[Authorize]
[Produces("application/json")]
public sealed class OrdenesCompraController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdenesCompraController(IMediator mediator) => _mediator = mediator;

    // ── Queries ───────────────────────────────────────────────────────────

    /// <summary>Lista paginada de órdenes de compra con filtros opcionales.</summary>
    /// <remarks>Query params: pageNumber, pageSize, proveedorId, estado, fechaDesde (YYYY-MM-DD), fechaHasta.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:compras.ordenes.view")]
    [ProducesResponseType(typeof(ApiResponse<OrdenesCompraPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        int pageNumber = 1, pageSize = 20;
        if (Request.Query.TryGetValue("pageNumber", out var pnv) && int.TryParse(pnv, out var pni)) pageNumber = pni;
        if (Request.Query.TryGetValue("pageSize",   out var psv) && int.TryParse(psv, out var psi)) pageSize   = psi;

        Guid? proveedorId = null;
        if (Request.Query.TryGetValue("proveedorId", out var prov) && Guid.TryParse(prov, out var prid)) proveedorId = prid;

        var estado = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;

        DateTime? desde = null, hasta = null;
        if (Request.Query.TryGetValue("fechaDesde", out var fdv) && DateTime.TryParse(fdv, out var fd)) desde = fd;
        if (Request.Query.TryGetValue("fechaHasta", out var fhv) && DateTime.TryParse(fhv, out var fh)) hasta = fh;

        var result = await _mediator.Send(
            new GetOrdenesCompraListQuery(pageNumber, pageSize, proveedorId, estado, desde, hasta), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>
    /// Lista de OC aprobadas o con recepción parcial que tienen saldo pendiente por facturar.
    /// Útil para el selector al vincular facturas electrónicas.
    /// </summary>
    [HttpGet("pendientes-por-facturar")]
    [Authorize(Policy = "perm:compras.ordenes.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OrdenCompraDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendientesPorFacturar(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrdenesPendientesPorFacturarQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Retorna el detalle completo de una OC (con líneas y facturas vinculadas).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:compras.ordenes.view")]
    [ProducesResponseType(typeof(ApiResponse<OrdenCompraDetailDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrdenCompraByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── Crear ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una OC en estado Borrador.
    /// </summary>
    /// <response code="201">OC creada en estado Borrador.</response>
    /// <response code="400">Datos inválidos o proveedor no encontrado.</response>
    [HttpPost]
    [Authorize(Policy = "perm:compras.ordenes.create")]
    [ProducesResponseType(typeof(ApiResponse<OrdenCompraDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearOrdenCompraCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // ── Transiciones de estado ────────────────────────────────────────────

    /// <summary>
    /// Envía la OC al proveedor (Borrador → Enviada).
    /// </summary>
    [HttpPatch("{id:guid}/enviar")]
    [Authorize(Policy = "perm:compras.ordenes.send")]
    [ProducesResponseType(typeof(ApiResponse<OrdenCompraDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enviar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnviarOrdenCompraCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviada");
    }

    /// <summary>
    /// Aprueba la OC (Borrador/Enviada → Aprobada).
    /// Solo una OC aprobada puede recibir facturas vinculadas.
    /// </summary>
    [HttpPatch("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:compras.ordenes.approve")]
    [ProducesResponseType(typeof(ApiResponse<OrdenCompraDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Aprobar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AprobarOrdenCompraCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Aprobada");
    }

    /// <summary>
    /// Cancela la OC. No posible en Cerrada o ya Cancelada.
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [Authorize(Policy = "perm:compras.ordenes.cancel")]
    [ProducesResponseType(typeof(ApiResponse<OrdenCompraDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelarOrdenCompraCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Cancelada");
    }

    /// <summary>
    /// Vincula una factura electrónica aprobada a esta OC.
    /// Actualiza las cantidades facturadas y cierra la OC si está completamente cubierta.
    /// </summary>
    /// <response code="200">Factura vinculada. OC actualizada (estado puede cambiar a RecibidaParcial o Cerrada).</response>
    /// <response code="400">Factura no aprobada, ya vinculada, o cantidades excedidas.</response>
    [HttpPost("{id:guid}/vincular-factura")]
    [Authorize(Policy = "perm:compras.ordenes.link-invoice")]
    [ProducesResponseType(typeof(ApiResponse<OrdenCompraDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VincularFactura(
        Guid id,
        [FromBody] VincularFacturaRequest body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new VincularFacturaAOrdenCompraCommand(id, body.CompraFacturaId), ct);
        return this.ToOkOrBadRequest(result, "Factura vinculada");
    }
}

/// <param name="CompraFacturaId">ID de la factura de compra aprobada a vincular.</param>
public sealed record VincularFacturaRequest(Guid CompraFacturaId);
