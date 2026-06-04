using ERP.Application.Common;
using ERP.Domain.Modules.Costing.Entities;
using ERP.Domain.Modules.Costing.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Costing;

public sealed class CostLayerRepository : ICostLayerRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;

    public CostLayerRepository(ErpDbContext context, ICurrentSubscriber subscriber, ICurrentCompany company)
    {
        _context    = context;
        _subscriber = subscriber;
        _company    = company;
    }

    private IQueryable<CostLayer> Scoped()
        => _context.CostLayers
            .Where(x => x.SubscriberId == _subscriber.SubscriberId)
            .Where(x => x.CompanyId == _company.CompanyId);

    public async Task<CostLayer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Scoped().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CostLayer>> GetFifoLayersAsync(
        Guid itemId,
        Guid warehouseId,
        Guid? variantId = null,
        CancellationToken ct = default)
        => await Scoped()
            .Where(x =>
                x.ItemId      == itemId       &&
                x.WarehouseId == warehouseId  &&
                x.VariantId   == variantId    &&
                !x.IsExhausted)
            // FIFO order — index ix_cost_layers_fifo covers this
            .OrderBy(x => x.LayerDate)
            .ToListAsync(ct);

    public async Task<(decimal TotalQty, decimal TotalCost)> GetAvcoSummaryAsync(
        Guid itemId,
        Guid warehouseId,
        Guid? variantId = null,
        CancellationToken ct = default)
    {
        var result = await Scoped()
            .Where(x =>
                x.ItemId      == itemId       &&
                x.WarehouseId == warehouseId  &&
                x.VariantId   == variantId    &&
                !x.IsExhausted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalQty  = g.Sum(x => x.RemainingQty),
                TotalCost = g.Sum(x => x.RemainingQty * x.UnitCost),
            })
            .FirstOrDefaultAsync(ct);

        return result is null
            ? (0m, 0m)
            : (result.TotalQty, result.TotalCost);
    }

    public async Task AddAsync(CostLayer layer, CancellationToken ct = default)
        => await _context.CostLayers.AddAsync(layer, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
