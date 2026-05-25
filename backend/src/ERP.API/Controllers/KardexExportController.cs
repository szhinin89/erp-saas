using ERP.API.Contracts;
using ERP.API.Export;
using ERP.API.Extensions;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.GetKardex;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/inventory/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexExportController : ControllerBase
{
    private readonly IMediator _mediator;

    public KardexExportController(IMediator mediator) => _mediator = mediator;

    [HttpGet("exportar/excel")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> ExportExcel(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = KardexQueryParameters.Parse(Request.Query);
        if (!ok) return err!;

        var result = await _mediator.Send(new GetKardexQuery(productId, warehouseId, startDate, endDate), ct);
        if (!result.IsSuccess) return BadRequest(result.Error);

        var bytes = KardexExcelExporter.Generate(result.Value!, startDate, endDate);
        var fileName = KardexAsyncHelper.BuildFileName(result.Value!, startDate, endDate, "xlsx");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("exportar/pdf")]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [Produces("application/pdf")]
    public async Task<IActionResult> ExportPdf(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = KardexQueryParameters.Parse(Request.Query);
        if (!ok) return err!;

        var result = await _mediator.Send(new GetKardexQuery(productId, warehouseId, startDate, endDate), ct);
        if (!result.IsSuccess) return BadRequest(result.Error);

        var bytes = KardexPdfExporter.Generate(result.Value!, startDate, endDate);
        var fileName = KardexAsyncHelper.BuildFileName(result.Value!, startDate, endDate, "pdf");
        return File(bytes, "application/pdf", fileName);
    }
}
