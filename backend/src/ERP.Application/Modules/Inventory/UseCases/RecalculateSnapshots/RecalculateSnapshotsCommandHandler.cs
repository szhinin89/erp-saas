using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Inventory.UseCases.RecalculateSnapshots;

public sealed class RecalculateSnapshotsCommandHandler
    : IRequestHandler<RecalculateSnapshotsCommand, Result<int>>
{
    private readonly IKardexSnapshotCalculator                     _calculator;
    private readonly ICurrentSubscriber                                _subscriber;
    private readonly ILogger<RecalculateSnapshotsCommandHandler>    _logger;

    public RecalculateSnapshotsCommandHandler(
        IKardexSnapshotCalculator                    calculator,
        ICurrentSubscriber subscriber,
        ILogger<RecalculateSnapshotsCommandHandler>   logger)
    {
        _calculator = calculator;
        _subscriber = subscriber;
        _logger     = logger;
    }

    public async Task<Result<int>> Handle(
        RecalculateSnapshotsCommand command, CancellationToken ct)
    {
        var subscriberId   = _subscriber.SubscriberId;
        var untilDate = command.DateTo?.Date ?? DateTime.UtcNow.Date.AddDays(-1);

        _logger.LogInformation(
            "RecalculateSnapshots: tenant={T}, productoId={P}, bodegaId={B}, hasta={H:yyyy-MM-dd}",
            subscriberId, command.ProductId, command.WarehouseId, untilDate);

        try
        {
            var count = await _calculator.RecalcularSubscriberAsync(
                subscriberId, command.ProductId, command.WarehouseId, untilDate, ct);

            _logger.LogInformation("RecalculateSnapshots: {Count} snapshots generados.", count);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recalcular snapshots.");
            return Result<int>.Failure($"Error al recalcular snapshots: {ex.Message}");
        }
    }
}
