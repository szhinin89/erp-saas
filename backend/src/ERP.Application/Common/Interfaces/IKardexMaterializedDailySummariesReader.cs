namespace ERP.Application.Common.Interfaces;

public sealed record KardexMvDayAggregate(
    DateOnly Date,
    decimal  EntryQty,
    decimal  EntryValue,
    decimal  ExitQty,
    decimal  ExitValue);

public interface IKardexMaterializedDailySummariesReader
{
    Task<IReadOnlyList<KardexMvDayAggregate>?> TryGetDailyAggregatesAsync(
        Guid subscriberId,
        Guid productId,
        Guid warehouseId,
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);
}
