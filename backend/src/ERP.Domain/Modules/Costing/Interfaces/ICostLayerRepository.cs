using ERP.Domain.Modules.Costing.Entities;

namespace ERP.Domain.Modules.Costing.Interfaces;

public interface ICostLayerRepository
{
    Task<CostLayer?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Capas no agotadas ordenadas por LayerDate ASC para FIFO.
    /// Usar con Take(n) para consumir en orden.
    /// </summary>
    Task<IReadOnlyList<CostLayer>> GetFifoLayersAsync(
        Guid itemId,
        Guid warehouseId,
        Guid? variantId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Costo promedio ponderado: retorna (totalQty, totalCost) de capas no agotadas.
    /// El caller calcula unitCost = totalCost / totalQty.
    /// </summary>
    Task<(decimal TotalQty, decimal TotalCost)> GetAvcoSummaryAsync(
        Guid itemId,
        Guid warehouseId,
        Guid? variantId = null,
        CancellationToken ct = default);

    Task AddAsync(CostLayer layer, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
