using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Application.Inventory.UseCases.EnqueueKardexReport;

public sealed class EnqueueKardexReportCommandHandler
    : IRequestHandler<EnqueueKardexReportCommand, Result<EnqueueKardexReportResult>>
{
    private readonly ICurrentSubscriber _subscriber;
    private readonly IKardexReportEnqueueService _enqueue;

    public EnqueueKardexReportCommandHandler(
        ICurrentSubscriber subscriber,
        IKardexReportEnqueueService enqueue)
    {
        _subscriber = subscriber;
        _enqueue = enqueue;
    }

    public async Task<Result<EnqueueKardexReportResult>> Handle(
        EnqueueKardexReportCommand command,
        CancellationToken ct)
    {
        var jobId = await _enqueue.EnqueueAsync(
            _subscriber.SubscriberId,
            command.ProductId,
            command.WarehouseId,
            command.StartDate,
            command.EndDate,
            ct);

        return Result<EnqueueKardexReportResult>.Success(new EnqueueKardexReportResult(
            jobId,
            KardexReport.StatusPending,
            $"El reporte está siendo procesado. Consulte el resultado en /api/inventory/kardex/resultado/{jobId}"));
    }
}
