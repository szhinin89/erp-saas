using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;
using ERP.Application.Modules.Purchasing.UseCases.CrearCompra;
using ERP.Application.Modules.Purchasing.UseCases.GetCompraById;
using ERP.Application.Modules.Purchasing.UseCases.GetCompras;
using ERP.Application.Modules.Purchasing.UseCases.RechazarCompra;
using ERP.Application.Modules.Purchasing.UseCases.ValidarCompra;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// GestiÃ³n del submÃ³dulo de Compras.
/// Flujo: Borrador â†’ (Validado) â†’ IsApproved | Rechazado.
/// </summary>
[AppFeature("Compras", "perm:compras.facturas.view", "ðŸ›’", "/compras", null, 45)]
[ApiController]
[Route("api/Compras")]
[Authorize]
[Produces("application/json")]
public sealed class PurchasesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchasesController(IMediator mediator) => _mediator = mediator;

    // â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Lista facturas de compra con filtros opcionales.</summary>
    /// <remarks>Query params: estado, proveedorId, desde (YYYY-MM-DD), hasta, search.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:compras.facturas.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PurchBillDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        PurchaseStatus? estado = null;
        if (Request.Query.TryGetValue("estado", out var ev) &&
            Enum.TryParse<PurchaseStatus>(ev, out var ep)) estado = ep;

        Guid? proveedorId = null;
        if (Request.Query.TryGetValue("proveedorId", out var pv) &&
            Guid.TryParse(pv, out var pid)) proveedorId = pid;

        DateTime? desde = null, hasta = null;
        if (Request.Query.TryGetValue("desde", out var dv) && DateTime.TryParse(dv, out var d)) desde = d;
        if (Request.Query.TryGetValue("hasta", out var hv) && DateTime.TryParse(hv, out var h)) hasta = h;

        var search = CatalogQueryParameters.ParseSearch(Request.Query);

        var result = await _mediator.Send(
            new GetPurchasesQuery(estado, proveedorId, desde, hasta, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<PurchBillDto>());
    }

    /// <summary>Retorna el detalle completo de una factura de compra (con lÃ­neas).</summary>
    /// <response code="404">La compra no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:compras.facturas.view")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPurchaseByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    // â”€â”€ Crear â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Crea una factura de compra en modo Manual.</summary>
    /// <response code="201">Compra creada en estado Borrador.</response>
    /// <response code="422">Datos invÃ¡lidos.</response>
    [HttpPost("manual")]
    [Authorize(Policy = "perm:compras.facturas.create")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateManual(
        [FromBody] CreatePurchaseCommand command, CancellationToken ct = default)
    {
        var cmd = command with { Modo = PurchaseCreationMode.Manual };
        var result = await _mediator.Send(cmd, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>
    /// Crea una factura de compra a partir de un XML del SRI Ecuador.
    /// Sube el archivo como multipart/form-data con campo 'xmlFile'.
    /// Si el proveedor (por RUC) no existe en el tenant, se crea automÃ¡ticamente.
    /// </summary>
    /// <response code="201">Compra creada en estado Borrador.</response>
    /// <response code="400">Clave de acceso duplicada o error en el XML.</response>
    /// <response code="422">XML vacÃ­o o invÃ¡lido.</response>
    [HttpPost("xml")]
    [Authorize(Policy = "perm:compras.facturas.create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateFromXml(
        IFormFile xmlFile, CancellationToken ct = default)
    {
        if (xmlFile is null || xmlFile.Length == 0)
            return this.ApiBadRequest("Debe adjuntar el archivo XML de la factura.");

        byte[] content;
        await using var ms = new MemoryStream();
        await xmlFile.CopyToAsync(ms, ct);
        content = ms.ToArray();

        var command = new CreatePurchaseCommand(
            Modo: PurchaseCreationMode.Xml,
            XmlContent: content,
            XmlFileName: xmlFile.FileName,
            SupplierId: null, InvoiceNumber: null, InvoiceDate: null,
            DueDate: null, PaymentTerms: null,
            Notes: null, Lines: null,
            WarehouseAllocations: null);

        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    // â”€â”€ Transiciones de estado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Valida una compra en Borrador: verifica proveedor activo, totales y clave de acceso.
    /// Cambia estado a Validado.
    /// </summary>
    /// <response code="200">Compra validada.</response>
    /// <response code="400">Error de validaciÃ³n de negocio.</response>
    [HttpPatch("{id:guid}/validar")]
    [Authorize(Policy = "perm:compras.facturas.validate")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ValidatePurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Validado");
    }

    /// <summary>
    /// Aprueba una compra Validada. Genera el asiento contable si hay cuentas configuradas.
    /// Requiere rol Revisor o Admin.
    /// </summary>
    /// <response code="200">Compra aprobada (con o sin asiento contable).</response>
    /// <response code="400">La compra no estÃ¡ en estado Validado.</response>
    [HttpPatch("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:compras.facturas.approve")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ApprovePurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "IsApproved");
    }

    /// <summary>Rechaza una compra (Borrador o Validada) con un motivo obligatorio.</summary>
    /// <response code="200">Compra rechazada.</response>
    /// <response code="400">La compra ya estÃ¡ Aprobada o Rechazada.</response>
    [HttpPatch("{id:guid}/rechazar")]
    [Authorize(Policy = "perm:compras.facturas.reject")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectPurchaseRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RejectPurchaseCommand(id, request.Reason), ct);
        return this.ToOkOrBadRequest(result, "Rechazado");
    }
}

/// <summary>Cuerpo del request de rechazo.</summary>
public sealed record RejectPurchaseRequest(string Reason);

