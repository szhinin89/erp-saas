using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Ventas.DTOs;
using ERP.Application.Ventas.UseCases.AnularFactura;
using ERP.Application.Ventas.UseCases.CrearVenta;
using ERP.Application.Ventas.UseCases.EmitirFacturaElectronica;
using ERP.Application.Ventas.UseCases.GetStockDisponibleParaVenta;
using ERP.Application.Ventas.UseCases.GetVentaById;
using ERP.Application.Ventas.UseCases.GetVentasList;
using ERP.Application.Ventas.UseCases.ReintentarEnvio;
using ERP.Application.Ventas.UseCases.ValidarVenta;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión del módulo de Ventas — Facturación Electrónica SRI Ecuador.
/// Flujo: Borrador → Validado → Autorizado | Rechazado | ErrorEnvio.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class VentasController : ControllerBase
{
    private readonly IMediator _mediator;

    public VentasController(IMediator mediator) => _mediator = mediator;

    // ── Queries ───────────────────────────────────────────────────────────

    /// <summary>Lista facturas de venta paginadas con filtros opcionales.</summary>
    /// <remarks>Query params: pageNumber, pageSize, clienteId, desde (YYYY-MM-DD), hasta, estado, search.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:ventas.facturas.view")]
    [ProducesResponseType(typeof(ApiResponse<VentasPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        int pageNumber = 1, pageSize = 20;
        if (Request.Query.TryGetValue("pageNumber", out var pnv) && int.TryParse(pnv, out var pni)) pageNumber = pni;
        if (Request.Query.TryGetValue("pageSize",   out var psv) && int.TryParse(psv, out var psi)) pageSize   = psi;

        Guid? clienteId = null;
        if (Request.Query.TryGetValue("clienteId", out var cv) && Guid.TryParse(cv, out var cid)) clienteId = cid;

        DateTime? desde = null, hasta = null;
        if (Request.Query.TryGetValue("desde", out var dv) && DateTime.TryParse(dv, out var d)) desde = d;
        if (Request.Query.TryGetValue("hasta", out var hv) && DateTime.TryParse(hv, out var h)) hasta = h;

        var estado = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;
        var search = CatalogQueryParameters.ParseSearch(Request.Query);

        var result = await _mediator.Send(
            new GetVentasListQuery(pageNumber, pageSize, clienteId, desde, hasta, estado, search), ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>Retorna el detalle completo de una factura de venta (con líneas).</summary>
    /// <response code="404">La factura no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:ventas.facturas.view")]
    [ProducesResponseType(typeof(ApiResponse<VentasFacturaDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetVentaByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Consulta el stock disponible de un producto en una bodega.</summary>
    /// <remarks>Query params requeridos: productoId, bodegaId.</remarks>
    [HttpGet("stock")]
    [Authorize(Policy = "perm:ventas.stock.view")]
    [ProducesResponseType(typeof(ApiResponse<StockDisponibleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStock(CancellationToken ct = default)
    {
        if (!Request.Query.TryGetValue("productoId", out var pv) || !Guid.TryParse(pv, out var productoId))
            return this.ApiBadRequest("El parámetro productoId es requerido y debe ser un GUID válido.");
        if (!Request.Query.TryGetValue("bodegaId",   out var bv) || !Guid.TryParse(bv, out var bodegaId))
            return this.ApiBadRequest("El parámetro bodegaId es requerido y debe ser un GUID válido.");

        var result = await _mediator.Send(new GetStockDisponibleParaVentaQuery(productoId, bodegaId), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ── Crear ─────────────────────────────────────────────────────────────

    /// <summary>Crea una nueva factura de venta en estado Borrador.</summary>
    /// <response code="201">Factura creada. Retorna el ID.</response>
    /// <response code="400">Stock insuficiente, cliente o bodega inválida.</response>
    [HttpPost]
    [Authorize(Policy = "perm:ventas.facturas.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearVentaCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // ── Transiciones de estado ────────────────────────────────────────────

    /// <summary>
    /// Valida una factura en Borrador: verifica totales y detalles.
    /// Cambia estado a Validado.
    /// </summary>
    [HttpPatch("{id:guid}/validar")]
    [Authorize(Policy = "perm:ventas.facturas.validate")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ValidarVentaCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Validado");
    }

    /// <summary>
    /// Emite la factura al SRI Ecuador: genera XML, firma y envía.
    /// Si es autorizada, descuenta inventario y crea asiento contable.
    /// Estado resultante: Autorizado | Rechazado | ErrorEnvio.
    /// </summary>
    [HttpPatch("{id:guid}/emitir")]
    [Authorize(Policy = "perm:ventas.facturas.emit")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Emitir(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EmitirFacturaElectronicaCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Emitido");
    }

    /// <summary>
    /// Reintenta el envío al SRI para facturas en ErrorEnvio o Rechazado.
    /// Resetea el estado y ejecuta el flujo de emisión completo.
    /// </summary>
    [HttpPatch("{id:guid}/reintentar")]
    [Authorize(Policy = "perm:ventas.facturas.emit")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reintentar(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ReintentarEnvioCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Reintentado");
    }

    /// <summary>
    /// Anula la factura. Solo posible en estados Borrador, Validado, Rechazado o ErrorEnvio.
    /// Las facturas ya autorizadas por el SRI no pueden anularse directamente.
    /// </summary>
    [HttpPatch("{id:guid}/anular")]
    [Authorize(Policy = "perm:ventas.facturas.cancel")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Anular(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AnularFacturaCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Anulado");
    }
}
