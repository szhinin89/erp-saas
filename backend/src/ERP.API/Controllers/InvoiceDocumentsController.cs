using ERP.API.Attributes;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Impresión y RIDE PDF para facturas y notas de venta.</summary>
[AppFeature("Ventas", "perm:sales.invoices.view", "🧾", "/sales/invoices", null, 50)]
[ApiController]
[Route("api/sales/invoices")]
[Authorize]
public sealed class InvoiceDocumentsController : ControllerBase
{
    private readonly IReceiptPrintService _tirillaFacturaService;
    private readonly IRideGeneratorService _rideGenerator;

    public InvoiceDocumentsController(
        IReceiptPrintService ReceiptPrintService,
        IRideGeneratorService rideGenerator)
    {
        _tirillaFacturaService = ReceiptPrintService;
        _rideGenerator = rideGenerator;
    }

    [HttpGet("{id:guid}/imprimir")]
    [Authorize(Policy = "perm:sales.invoices.view")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Print(Guid id, CancellationToken ct = default)
    {
        try
        {
            var html = await _tirillaFacturaService.GenerateInvoiceHtmlAsync(id, ct);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/ride")]
    [Authorize(Policy = "perm:sales.invoices.view")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRide(Guid id, CancellationToken ct = default)
    {
        try
        {
            var pdfBytes = await _rideGenerator.GenerateFacturaPdfAsync(id, ct);
            return File(pdfBytes, "application/pdf", $"RIDE_{id}.pdf");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("notas/{id:guid}/ride")]
    [Authorize(Policy = "perm:sales.invoices.view")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotaRide(Guid id, CancellationToken ct = default)
    {
        try
        {
            var pdfBytes = await _rideGenerator.GenerateNotaPdfAsync(id, ct);
            return File(pdfBytes, "application/pdf", $"RIDE_NOTA_{id}.pdf");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
