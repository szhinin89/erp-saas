using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.AprobarGasto;
using ERP.Application.Modules.Expenses.UseCases.CrearGasto;
using ERP.Application.Modules.Expenses.UseCases.GetGastoById;
using ERP.Application.Modules.Expenses.UseCases.GetGastos;
using ERP.Application.Modules.Expenses.UseCases.RechazarGasto;
using ERP.Application.Modules.Expenses.UseCases.ValidarGasto;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Facturas de gasto (registro manual o importaciÃ³n XML del SRI Ecuador).
/// Flujo de estados: Borrador â†’ Validado â†’ IsApproved | Rechazado. Requiere permisos <c>gastos.facturas.*</c> en el perfil (los roles Admin/SuperAdmin los omiten).
/// </summary>
[AppFeature("Gastos", "perm:gastos.facturas.view", "ðŸ’¸", "/gastos", null, 55)]
[ApiController]
[Route("api/Gastos")]
[Authorize]
[Produces("application/json")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpensesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista gastos con filtros opcionales (estado, proveedor, fechas, texto).</summary>
    /// <remarks>Query: <c>estado</c>, <c>proveedorId</c>, <c>desde</c>, <c>hasta</c>, <c>search</c>.</remarks>
    [HttpGet]
    [Authorize(Policy = "perm:gastos.facturas.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        ExpenseStatus? estado = null;
        if (Request.Query.TryGetValue("estado", out var ev) &&
            Enum.TryParse<ExpenseStatus>(ev, out var ep)) estado = ep;

        Guid? proveedorId = null;
        if (Request.Query.TryGetValue("proveedorId", out var pv) &&
            Guid.TryParse(pv, out var pid)) proveedorId = pid;

        DateTime? desde = null, hasta = null;
        if (Request.Query.TryGetValue("desde", out var dv) && DateTime.TryParse(dv, out var d)) desde = d;
        if (Request.Query.TryGetValue("hasta", out var hv) && DateTime.TryParse(hv, out var h)) hasta = h;

        var search = CatalogQueryParameters.ParseSearch(Request.Query);

        var result = await _mediator.Send(
            new GetExpensesQuery(estado, proveedorId, desde, hasta, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ExpenseInvoiceDto>());
    }

    /// <summary>Obtiene un gasto por id (tenant actual).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:gastos.facturas.view")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetExpenseByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Crea un gasto en modo manual (Borrador). Totales altos exigen XML.</summary>
    /// <response code="201">Gasto creado.</response>
    [HttpPost("manual")]
    [Authorize(Policy = "perm:gastos.facturas.create")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateManual(
        [FromBody] CreateExpenseCommand command,
        CancellationToken ct = default)
    {
        var cmd = command with { Modo = ExpenseCreationMode.Manual };
        var result = await _mediator.Send(cmd, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Importa un gasto desde XML SRI (multipart: archivo <c>xmlFile</c>, form <c>categoriaGasto</c> obligatorio).</summary>
    /// <response code="201">Gasto creado en Borrador.</response>
    /// <response code="400">XML invÃ¡lido o clave duplicada.</response>
    [HttpPost("xml")]
    [Authorize(Policy = "perm:gastos.facturas.create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromXml(
        IFormFile xmlFile,
        [FromForm] string categoriaGasto,
        [FromForm] string? observaciones,
        CancellationToken ct = default)
    {
        if (xmlFile is null || xmlFile.Length == 0)
            return this.ApiBadRequest("Debe adjuntar el archivo XML del comprobante.");

        await using var ms = new MemoryStream();
        await xmlFile.CopyToAsync(ms, ct);
        var content = ms.ToArray();

        var command = new CreateExpenseCommand(
            Modo: ExpenseCreationMode.Xml,
            XmlContent: content,
            XmlFileName: xmlFile.FileName,
            SupplierId: null,
            IssueDate: null,
            Concept: null,
            Category: categoriaGasto,
            Subtotal: null,
            VatTotal: null,
            Total: null,
            Notes: observaciones);

        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Valida un gasto en Borrador y pasa a estado Validado.</summary>
    [HttpPatch("{id:guid}/validar")]
    [Authorize(Policy = "perm:gastos.facturas.validate")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ValidateExpenseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Validado");
    }

    /// <summary>Aprueba un gasto Validado y genera asiento contable si aplica.</summary>
    [HttpPatch("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:gastos.facturas.approve")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ApproveExpenseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "IsApproved");
    }

    /// <summary>Rechaza un gasto con motivo obligatorio.</summary>
    [HttpPatch("{id:guid}/rechazar")]
    [Authorize(Policy = "perm:gastos.facturas.reject")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectExpenseRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RejectExpenseCommand(id, request.Reason), ct);
        return this.ToOkOrBadRequest(result, "Rechazado");
    }
}

public sealed record RejectExpenseRequest(string Reason);



