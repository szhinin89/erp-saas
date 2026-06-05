using ERP.Application.Common.Config;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.EnqueueKardexReport;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ERP.API;

internal static class KardexAsyncHelper
{
    public static bool ShouldUseAsync(KardexOptions opts, DateTime? startDate, DateTime? endDate)
    {
        if (!opts.UseScalableMode || !opts.EnableAsyncReport) return false;
        if (startDate is null || endDate is null) return false;

        var days = (endDate.Value.Date - startDate.Value.Date).Days;
        return days > opts.MaxDaysForSync;
    }

    public static async Task<IActionResult> EnqueueAsync(
        IMediator mediator,
        HttpResponse response,
        Guid productId,
        Guid warehouseId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new EnqueueKardexReportCommand(productId, warehouseId, startDate, endDate), ct);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { mensaje = result.Error });

        response.Headers.Append("Retry-After", "10");
        return new ObjectResult(new
        {
            jobId = result.Value!.JobId,
            Status = result.Value.Status,
            mensaje = result.Value.Message,
        })
        { StatusCode = StatusCodes.Status202Accepted };
    }

    public static string BuildFileName(KardexResponse k, DateTime? from, DateTime? to, string ext)
    {
        var code = k.Producto.Codigo.Replace("/", "-");
        var period = from.HasValue || to.HasValue
            ? $"_{from?.ToString("yyyyMMdd") ?? "start"}-{to?.ToString("yyyyMMdd") ?? "today"}"
            : "";
        return $"Kardex_{code}{period}.{ext}";
    }
}
