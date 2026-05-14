using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.NotasProveedor;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Notas de crédito/débito recibidas de proveedores (SRI), aplicables a compras o gastos.</summary>
[Modulo("Notas proveedor", "perm:compras.notas-proveedor.view", "📄", "/compras/notas-proveedor", "perm:compras.facturas.view", 47)]
[ApiController]
[Route("api/compras/notas-proveedor")]
[Authorize]
[Produces("application/json")]
public sealed class ComprasNotasProveedorController : ControllerBase
{
    private readonly IMediator _mediator;

    public ComprasNotasProveedorController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:compras.notas-proveedor.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompraNotaProveedorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct = default)
    {
        Guid? proveedorId = null, compraFacturaId = null, gastoFacturaId = null;
        if (Request.Query.TryGetValue("proveedorId", out var pv) && Guid.TryParse(pv, out var pid))
            proveedorId = pid;
        if (Request.Query.TryGetValue("compraFacturaId", out var cv) && Guid.TryParse(cv, out var cid))
            compraFacturaId = cid;
        if (Request.Query.TryGetValue("gastoFacturaId", out var gv) && Guid.TryParse(gv, out var gid))
            gastoFacturaId = gid;
        var estado = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;

        var result = await _mediator.Send(
            new GetComprasNotasProveedorQuery(proveedorId, compraFacturaId, gastoFacturaId, estado), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<CompraNotaProveedorDto>());
    }

    /// <summary>Importa XML de nota (multipart campo <c>xmlFile</c>).</summary>
    [HttpPost]
    [Authorize(Policy = "perm:compras.notas-proveedor.create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CompraNotaProveedorDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Importar(
        IFormFile xmlFile,
        [FromForm] Guid? compraFacturaId,
        [FromForm] Guid? gastoFacturaId,
        CancellationToken ct = default)
    {
        if (xmlFile is null || xmlFile.Length == 0)
            return this.ApiBadRequest("Debe adjuntar el archivo XML de la nota.");

        await using var ms = new MemoryStream();
        await xmlFile.CopyToAsync(ms, ct);
        var content = ms.ToArray();

        var cmd = new ImportarCompraNotaProveedorCommand(
            content,
            xmlFile.FileName,
            compraFacturaId,
            gastoFacturaId);
        var result = await _mediator.Send(cmd, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:compras.notas-proveedor.approve")]
    [ProducesResponseType(typeof(ApiResponse<CompraNotaProveedorDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Aprobar(
        Guid id,
        [FromBody] AprobarCompraNotaProveedorBody? body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new AprobarCompraNotaProveedorCommand(
                id,
                body?.NumeroAutorizacion,
                body?.FechaAutorizacion),
            ct);
        return this.ToOkOrBadRequest(result, "Aprobado");
    }
}

public sealed class AprobarCompraNotaProveedorBody
{
    public string? NumeroAutorizacion { get; set; }
    public DateTime? FechaAutorizacion { get; set; }
}
