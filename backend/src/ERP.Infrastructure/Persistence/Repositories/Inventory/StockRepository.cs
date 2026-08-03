using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class StockRepository : IStockRepository
{
    /// <summary>
    /// Reintentos máximos ante colisión de SequenceNumber (xmin de CurrentStock o violación del
    /// índice UNIQUE de secuencia). Centralizado aquí — no configurable por dominio de negocio.
    /// </summary>
    public const int MaxSequenceRetryAttempts = 3;

    private const string SequenceUniqueConstraintName =
        "uq_stock_movements_company_product_warehouse_sequence";

    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;
    private readonly IDatabaseExceptionTranslator _exceptionTranslator;

    private sealed record PendingMovement(
        Guid TenantId,
        Guid CompanyId,
        Guid ProductId,
        Guid WarehouseId,
        StockMovementType MovementType,
        decimal Quantity,
        string UomCode,
        DateOnly EffectiveDate,
        string? Reference,
        Guid? SourceDocId,
        string? SourceDocType,
        Guid ActorId,
        decimal? UnitCost,
        Guid? LotId,
        Guid? SerialId,
        Guid? SourceDocLineId
    );

    private readonly List<PendingMovement> _pending = new();

    public StockRepository(
        ErpDbContext db,
        ICurrentCompany company,
        IDatabaseExceptionTranslator exceptionTranslator
    )
    {
        _db = db;
        _company = company;
        _exceptionTranslator = exceptionTranslator;
    }

    public Task<CurrentStock?> GetStockAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid productId,
        CancellationToken ct = default
    ) =>
        _db.Set<CurrentStock>()
            .FirstOrDefaultAsync(
                s =>
                    s.TenantId == tenantId
                    && s.WarehouseId == warehouseId
                    && s.ProductId == productId,
                ct
            );

    public async Task<IReadOnlyList<CurrentStock>> GetStockByWarehouseAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid? productId,
        CancellationToken ct = default
    )
    {
        var q = _db.Set<CurrentStock>()
            .Where(s => s.TenantId == tenantId && s.WarehouseId == warehouseId);
        if (productId.HasValue)
            q = q.Where(s => s.ProductId == productId.Value);
        return await q.ToListAsync(ct);
    }

    public Task AddCurrentStockAsync(CurrentStock entity, CancellationToken ct = default) =>
        _db.Set<CurrentStock>().AddAsync(entity, ct).AsTask();

    public async Task<StockMovement> AppendMovementAsync(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        StockMovementType movementType,
        decimal quantity,
        string uomCode,
        DateOnly effectiveDate,
        string? reference,
        Guid? sourceDocId,
        string? sourceDocType,
        Guid actorId,
        decimal? unitCost = null,
        Guid? lotId = null,
        Guid? serialId = null,
        CancellationToken ct = default,
        Guid? sourceDocLineId = null
    )
    {
        var request = new PendingMovement(
            tenantId,
            companyId,
            productId,
            warehouseId,
            movementType,
            quantity,
            uomCode,
            effectiveDate,
            reference,
            sourceDocId,
            sourceDocType,
            actorId,
            unitCost,
            lotId,
            serialId,
            sourceDocLineId
        );

        var movement = await CreateAndTrackMovementAsync(request, ct);
        _pending.Add(request);
        return movement;
    }

    public async Task<int> SaveChangesWithSequenceRetryAsync(CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var result = await _db.SaveChangesAsync(ct);
                _pending.Clear();
                return result;
            }
            catch (Exception ex) when (attempt < MaxSequenceRetryAttempts && IsSequenceConflict(ex))
            {
                await RecoverFromConflictAndRetrackAsync(ct);
            }
        }
    }

    /// <summary>
    /// Calcula y trackea un StockMovement + su efecto sobre CurrentStock, sin llamar a SaveChanges.
    /// SequenceNumber/RunningAverageCost/RunningStockValue se derivan exclusivamente del último
    /// StockMovement real de la clave (Company/Product/Warehouse) — nunca de CurrentStock.
    /// </summary>
    private async Task<StockMovement> CreateAndTrackMovementAsync(
        PendingMovement r,
        CancellationToken ct
    )
    {
        var stock =
            await GetStockAsync(r.TenantId, r.WarehouseId, r.ProductId, ct)
            ?? _db.ChangeTracker.Entries<CurrentStock>()
                .Select(e => e.Entity)
                .FirstOrDefault(s =>
                    s.TenantId == r.TenantId
                    && s.WarehouseId == r.WarehouseId
                    && s.ProductId == r.ProductId
                );

        if (stock is null)
        {
            stock = CurrentStock.Create(
                r.TenantId,
                r.ProductId,
                r.WarehouseId,
                r.ActorId,
                r.CompanyId
            );
            await AddCurrentStockAsync(stock, ct);
        }

        var previousQty = stock.Quantity;

        var last = await _db.Set<StockMovement>()
            .Where(m =>
                m.CompanyId == r.CompanyId
                && m.ProductId == r.ProductId
                && m.WarehouseId == r.WarehouseId
            )
            .OrderByDescending(m => m.SequenceNumber)
            .Select(m => new
            {
                m.SequenceNumber,
                m.RunningAverageCost,
                m.RunningStockValue,
            })
            .FirstOrDefaultAsync(ct);

        var nextSeq = (last?.SequenceNumber ?? 0) + 1;
        var lastRunningValue = last?.RunningStockValue ?? 0m;
        var lastRunningAvg = last?.RunningAverageCost ?? 0m;

        // Salidas sin costo explícito consumen el costo promedio corrido del propio Kardex
        // (nunca CurrentStock.AverageCost, que es solo una proyección derivada).
        var resolvedUnitCost = r.UnitCost ?? lastRunningAvg;
        var resultQty = previousQty + r.Quantity;
        var newRunningStockValue = Math.Max(0m, lastRunningValue + r.Quantity * resolvedUnitCost);
        var newRunningAverageCost = resultQty > 0m ? newRunningStockValue / resultQty : 0m;

        // Branch Ownership: el movimiento pertenece a la sucursal dueña de la bodega afectada,
        // no a la sucursal de sesión activa del operador — en una transferencia inter-sucursal,
        // el movimiento del lado destino debe llevar la sucursal de la bodega destino.
        var branchId = await _db.Set<Warehouse>()
            .Where(w => w.Id == r.WarehouseId)
            .Select(w => w.BranchId)
            .FirstOrDefaultAsync(ct);
        if (branchId == Guid.Empty)
            throw new InvalidOperationException(
                $"No se pudo resolver la sucursal de la bodega '{r.WarehouseId}' para registrar el movimiento de Kardex."
            );

        var movement = StockMovement.Create(
            r.TenantId,
            branchId,
            r.ProductId,
            r.WarehouseId,
            r.MovementType,
            r.Quantity,
            r.UomCode,
            previousQty,
            nextSeq,
            newRunningAverageCost,
            newRunningStockValue,
            r.EffectiveDate,
            r.Reference,
            r.SourceDocId,
            r.SourceDocType,
            r.ActorId,
            r.CompanyId,
            r.UnitCost,
            r.LotId,
            r.SerialId,
            r.SourceDocLineId
        );

        await _db.Set<StockMovement>().AddAsync(movement, ct);

        // CurrentStock se actualiza como proyección derivada del mismo hecho — no es su origen.
        stock.ApplyMovement(r.Quantity, r.ActorId, resolvedUnitCost);

        return movement;
    }

    private bool IsSequenceConflict(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
            return true;

        return _exceptionTranslator.TryGetUniqueViolation(ex, out var info)
            && info.ConstraintName == SequenceUniqueConstraintName;
    }

    private async Task RecoverFromConflictAndRetrackAsync(CancellationToken ct)
    {
        var toRetry = _pending.ToList();
        _pending.Clear();

        foreach (var entry in _db.ChangeTracker.Entries<StockMovement>().ToList())
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;

        foreach (var entry in _db.ChangeTracker.Entries<CurrentStock>().ToList())
            if (entry.State == EntityState.Modified)
                await entry.ReloadAsync(ct);

        foreach (var r in toRetry)
        {
            await CreateAndTrackMovementAsync(r, ct);
            _pending.Add(r);
        }
    }

    public async Task<IReadOnlyList<StockMovement>> GetMovementsAsync(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default
    )
    {
        var from = AsUtc(fromUtc);
        var to = AsUtc(toUtc);
        var q = _db.Set<StockMovement>()
            .Where(m =>
                m.TenantId == tenantId && m.ProductId == productId && m.WarehouseId == warehouseId
            );
        if (from.HasValue)
            q = q.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(m => m.CreatedAt <= to.Value);
        return await q.OrderByDescending(m => m.SequenceNumber).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockMovement>> GetMovementsByProductAsync(
        Guid tenantId,
        Guid productId,
        Guid? warehouseId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default
    )
    {
        var from = AsUtc(fromUtc);
        var to = AsUtc(toUtc);
        var q = _db.Set<StockMovement>()
            .Where(m => m.TenantId == tenantId && m.ProductId == productId);
        if (warehouseId.HasValue)
            q = q.Where(m => m.WarehouseId == warehouseId.Value);
        if (from.HasValue)
            q = q.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(m => m.CreatedAt <= to.Value);
        return await q.OrderBy(m => m.WarehouseId).ThenBy(m => m.SequenceNumber).ToListAsync(ct);
    }

    /// <summary>
    /// El model binder de ASP.NET produce DateTime.Kind=Unspecified para query params;
    /// Npgsql exige Kind=Utc para comparar contra columnas timestamptz (created_at).
    /// </summary>
    private static DateTime? AsUtc(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    public Task<StockMovement?> GetMovementByIdAsync(
        Guid tenantId,
        Guid movementId,
        CancellationToken ct = default
    ) =>
        _db.Set<StockMovement>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == movementId, ct);

    public async Task<IReadOnlyList<StockMovement>> GetMovementsByDocumentAsync(
        Guid tenantId,
        Guid sourceDocId,
        string sourceDocType,
        CancellationToken ct = default
    ) =>
        await _db.Set<StockMovement>()
            .Where(m =>
                m.TenantId == tenantId
                && m.SourceDocId == sourceDocId
                && m.SourceDocType == sourceDocType
            )
            .OrderBy(m => m.WarehouseId)
            .ThenBy(m => m.SequenceNumber)
            .ToListAsync(ct);

    public Task<StockMovement?> GetPreviousMovementAsync(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        long sequenceNumber,
        CancellationToken ct = default
    ) =>
        _db.Set<StockMovement>()
            .Where(m =>
                m.TenantId == tenantId
                && m.CompanyId == companyId
                && m.ProductId == productId
                && m.WarehouseId == warehouseId
                && m.SequenceNumber < sequenceNumber
            )
            .OrderByDescending(m => m.SequenceNumber)
            .FirstOrDefaultAsync(ct);

    public Task<StockMovement?> GetNextMovementAsync(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        long sequenceNumber,
        CancellationToken ct = default
    ) =>
        _db.Set<StockMovement>()
            .Where(m =>
                m.TenantId == tenantId
                && m.CompanyId == companyId
                && m.ProductId == productId
                && m.WarehouseId == warehouseId
                && m.SequenceNumber > sequenceNumber
            )
            .OrderBy(m => m.SequenceNumber)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CurrentStock>> GetStockByProductAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken ct = default
    ) =>
        await _db.Set<CurrentStock>()
            .Where(s => s.TenantId == tenantId && s.ProductId == productId)
            .ToListAsync(ct);

    public async Task<(decimal TotalQuantity, decimal TotalStockValue)> GetAggregatedStockAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken ct = default
    )
    {
        var result = await _db.Set<CurrentStock>()
            .Where(s => s.TenantId == tenantId && s.ProductId == productId && s.Quantity > 0)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalQty = g.Sum(s => s.Quantity),
                TotalVal = g.Sum(s => s.TotalStockValue),
            })
            .FirstOrDefaultAsync(ct);

        return result is null ? (0m, 0m) : (result.TotalQty, result.TotalVal);
    }

    public async Task<decimal?> GetLastPurchaseCostAsync(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        CancellationToken ct = default
    )
    {
        return await _db.Set<StockMovement>()
            .Where(m =>
                m.TenantId == tenantId
                && m.ProductId == productId
                && m.WarehouseId == warehouseId
                && m.MovementType == StockMovementType.PurchaseEntry
            )
            .OrderByDescending(m => m.SequenceNumber)
            .Select(m => m.UnitCost)
            .FirstOrDefaultAsync(ct);
    }
}
