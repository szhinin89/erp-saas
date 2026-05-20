using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Inventory.UseCases.RecalcularSnapshots;

public sealed class RecalcularSnapshotsCommandHandler
    : IRequestHandler<RecalcularSnapshotsCommand, Result<int>>
{
    private readonly IKardexSnapshotCalculator                     _calculator;
    private readonly ICurrentSubscriber                                _tenant;
    private readonly ILogger<RecalcularSnapshotsCommandHandler>    _logger;

    public RecalcularSnapshotsCommandHandler(
        IKardexSnapshotCalculator                    calculator,
        ICurrentSubscriber                               tenant,
        ILogger<RecalcularSnapshotsCommandHandler>   logger)
    {
        _calculator = calculator;
        _tenant     = tenant;
        _logger     = logger;
    }

    public async Task<Result<int>> Handle(
        RecalcularSnapshotsCommand command, CancellationToken ct)
    {
        var subscriberId   = _tenant.SubscriberId;
        var untilDate = command.DateTo?.Date ?? DateTime.UtcNow.Date.AddDays(-1);

        _logger.LogInformation(
            "RecalcularSnapshots: tenant={T}, productoId={P}, bodegaId={B}, hasta={H:yyyy-MM-dd}",
            subscriberId, command.ProductId, command.WarehouseId, untilDate);

        try
        {
            var count = await _calculator.RecalcularTenantAsync(
                subscriberId, command.ProductId, command.WarehouseId, untilDate, ct);

            _logger.LogInformation("RecalcularSnapshots: {Count} snapshots generados.", count);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recalcular snapshots.");
            return Result<int>.Failure($"Error al recalcular snapshots: {ex.Message}");
        }
    }
}
