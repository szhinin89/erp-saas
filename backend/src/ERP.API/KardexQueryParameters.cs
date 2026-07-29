using Microsoft.AspNetCore.Mvc;

namespace ERP.API;

internal static class KardexQueryParameters
{
    public static (
        bool Ok,
        Guid ProductId,
        Guid WarehouseId,
        DateTime? StartDate,
        DateTime? EndDate,
        IActionResult? Error
    ) Parse(IQueryCollection query)
    {
        if (!query.TryGetValue("productoId", out var pv) || !Guid.TryParse(pv, out var productId))
            return (
                false,
                default,
                default,
                null,
                null,
                new BadRequestObjectResult("productoId is required and must be a valid GUID.")
            );

        if (!query.TryGetValue("bodegaId", out var bv) || !Guid.TryParse(bv, out var warehouseId))
            return (
                false,
                default,
                default,
                null,
                null,
                new BadRequestObjectResult("bodegaId is required and must be a valid GUID.")
            );

        var (startDate, endDate) = ListQueryParameters.ParseDateTimeRange(
            query,
            "fechaInicio",
            "fechaFin"
        );
        return (true, productId, warehouseId, startDate, endDate, null);
    }
}
