using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Infrastructure.BackgroundServices;

namespace ERP.Infrastructure.BackgroundServices;

public sealed class KardexReportEnqueueService : IKardexReportEnqueueService
{
    private readonly IKardexReportRepository _repository;
    private readonly KardexReportQueue _queue;

    public KardexReportEnqueueService(IKardexReportRepository repository, KardexReportQueue queue)
    {
        _repository = repository;
        _queue      = queue;
    }

    public async Task<Guid> EnqueueAsync(
        Guid subscriberId,
        Guid productId,
        Guid warehouseId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken ct = default)
    {
        var report = KardexReport.Create(subscriberId, productId, warehouseId, startDate, endDate);
        await _repository.AddAsync(report, ct);
        await _repository.SaveChangesAsync(ct);
        await _queue.Writer.WriteAsync(report.Id, ct);
        return report.Id;
    }
}
