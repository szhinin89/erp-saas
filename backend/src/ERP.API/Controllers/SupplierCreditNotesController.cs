using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Notas de crédito/débito recibidas de proveedores (SRI), aplicables a compras o gastos.</summary>
[AppFeature("Notas proveedor", "perm:purchases.credit-notes.view", "🔄", "/purchases/credit-notes", "perm:purchases.invoices.view", 47)]
[ApiController]
[Route("api/purchases/credit-notes")]
[Authorize]
[Produces("application/json")]
public sealed class SupplierCreditNotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupplierCreditNotesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:purchases.credit-notes.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SupplierPurchaseNoteDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        Guid? proveedorId = null, compraFacturaId = null, gastoFacturaId = null;
        if (Request.Query.TryGetValue("proveedorId", out var pv) && Guid.TryParse(pv, out var pid))
            proveedorId = pid;
        if (Request.Query.TryGetValue("compraFacturaId", out var cv) && Guid.TryParse(cv, out var cid))
            compraFacturaId = cid;
        if (Request.Query.TryGetValue("gastoFacturaId", out var gv) && Guid.TryParse(gv, out var gid))
            gastoFacturaId = gid;
        var status = Request.Query.TryGetValue("estado", out var ev) ? ev.ToString() : null;

        var result = await _mediator.Send(
            new GetPurchaseSupplierNotesQuery(proveedorId, compraFacturaId, gastoFacturaId, status), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SupplierPurchaseNoteDto>());
    }

    /// <summary>Importa XML de nota (multipart campo <c>xmlFile</c>).</summary>
    [HttpPost]
    [Authorize(Policy = "perm:purchases.credit-notes.create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPurchaseNoteDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Import(
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

        var cmd = new ImportPurchaseSupplierNoteCommand(
            content,
            xmlFile.FileName,
            compraFacturaId,
            gastoFacturaId);
        var result = await _mediator.Send(cmd, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:purchases.credit-notes.approve")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPurchaseNoteDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApprovePurchaseSupplierNoteBody? body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new ApprovePurchaseSupplierNoteCommand(
                id,
                body?.AuthorizationNumber,
                body?.AuthorizationDate),
            ct);
        return this.ToOkOrBadRequest(result, "IsApproved");
    }
}

public sealed class ApprovePurchaseSupplierNoteBody
{
    public string? AuthorizationNumber { get; set; }
    public DateTime? AuthorizationDate { get; set; }
}
