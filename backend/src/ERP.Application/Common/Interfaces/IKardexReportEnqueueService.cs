namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Encola un reporte Kardex asíncrono (persistencia + canal en background).
/// </summary>
public interface IKardexReportEnqueueService
{
    Task<Guid> EnqueueAsync(
        Guid subscriberId,
        Guid productId,
        Guid warehouseId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken ct = default);
}
