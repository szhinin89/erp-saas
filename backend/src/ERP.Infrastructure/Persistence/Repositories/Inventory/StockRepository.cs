using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Companies;
using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Inventory.Policies;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

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
    private readonly ICompanyPrecisionPolicyProvider _precision;
    private readonly IOperationalPreferencesResolver? _preferences;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _ownedStockTransaction;

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
        Guid? SourceDocLineId,
        bool AllowNegativeSale,
        bool EnforceAvailability,
        decimal? TotalCost
    );

    private readonly List<PendingMovement> _pending = new();

    public StockRepository(
        ErpDbContext db,
        ICurrentCompany company,
        IDatabaseExceptionTranslator exceptionTranslator,
        ICompanyPrecisionPolicyProvider precision,
        IOperationalPreferencesResolver? preferences = null
    )
    {
        _precision = precision;
        _preferences = preferences;
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
            .ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == productId, ct);

    public async Task<IReadOnlyList<CurrentStock>> GetStockByWarehouseAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid? productId,
        CancellationToken ct = default
    )
    {
        var q = _db.Set<CurrentStock>()
            .ForOperationalScope(tenantId, _company)
            .Where(s => s.WarehouseId == warehouseId);
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
        Guid? sourceDocLineId = null,
        decimal? totalCost = null
    )
    {
        if (!_company.HasCompanyContext || companyId != _company.CompanyId)
            throw new ERP.Domain.Exceptions.DomainRuleViolationException("Empresa de inventario inválida.");
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == productId && i.TenantId == tenantId && i.CompanyId == companyId, ct);
        if (item is null || !item.ParticipatesInInventory)
            throw new ERP.Domain.Exceptions.DomainRuleViolationException("El ítem no es un producto de la empresa actual.");

        var preferences = _preferences is null ? null : await _preferences.ResolveAsync(ct);
        var enforce = SaleStockPolicy.RequiresAvailableStock(true,
            preferences?.Inventory.StockControlEnabled ?? true, item.StockConfig.StockControlEnabled,
            preferences?.SalesPos.AllowSellWithoutStock ?? false);

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
            sourceDocLineId,
            movementType == StockMovementType.SaleExit && !enforce,
            movementType == StockMovementType.SaleExit && enforce,
            totalCost
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
                if (_ownedStockTransaction is not null)
                {
                    await _ownedStockTransaction.CommitAsync(ct);
                    await _ownedStockTransaction.DisposeAsync();
                    _ownedStockTransaction = null;
                }
                _pending.Clear();
                return result;
            }
            // COMPRAS-METODO-ZH-01A2: retry only from a restored unit of work (failure before domain
            // events were published); a later failure is surfaced, never replayed half-applied.
            catch (Exception ex)
                when (attempt < MaxSequenceRetryAttempts
                    && IsSequenceConflict(ex)
                    && _db.LastSaveFailureIsRetryable
                )
            {
                await RecoverFromConflictAndRetrackAsync(ct);
            }
            catch
            {
                if (_ownedStockTransaction is not null)
                {
                    await _ownedStockTransaction.RollbackAsync(ct);
                    await _ownedStockTransaction.DisposeAsync();
                    _ownedStockTransaction = null;
                }
                throw;
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
        await LockStockResourceAsync(r, ct);
        var stock =
            await GetStockAsync(r.TenantId, r.WarehouseId, r.ProductId, ct)
            ?? _db.ChangeTracker.Entries<CurrentStock>()
                .Select(e => e.Entity)
                .FirstOrDefault(s =>
                    s.TenantId == r.TenantId
                    && s.CompanyId == r.CompanyId
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
                m.CostBasis,
            })
            .FirstOrDefaultAsync(ct);

        // Earlier lines in this unit of work have not reached the database yet.
        var pendingLast = _db
            .ChangeTracker.Entries<StockMovement>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .Where(m =>
                m.TenantId == r.TenantId
                && m.CompanyId == r.CompanyId
                && m.ProductId == r.ProductId
                && m.WarehouseId == r.WarehouseId
            )
            .OrderByDescending(m => m.SequenceNumber)
            .Select(m => new
            {
                m.SequenceNumber,
                m.RunningAverageCost,
                m.RunningStockValue,
                m.CostBasis,
            })
            .FirstOrDefault();
        if (
            pendingLast is not null
            && (last is null || pendingLast.SequenceNumber > last.SequenceNumber)
        )
            last = pendingLast;

        var nextSeq = (last?.SequenceNumber ?? 0) + 1;
        // INVENTORY-V1-B3: cantidad cero implica valor cero — un residuo de un movimiento histórico
        // en cantidad cero no se arrastra al siguiente (sin modificar el movimiento histórico).
        var lastRunningValue = previousQty == 0m ? 0m : last?.RunningStockValue ?? 0m;
        var lastRunningAvg = last?.RunningAverageCost ?? 0m;

        // Salidas sin costo explícito consumen el costo promedio corrido del propio Kardex
        // (nunca CurrentStock.AverageCost, que es solo una proyección derivada).
        decimal? basis = r.UnitCost ?? last?.CostBasis ?? (lastRunningAvg > 0m ? lastRunningAvg : null);
        if (basis is null)
            basis = await _db.Set<StockMovement>()
                .Where(m => m.TenantId == r.TenantId && m.CompanyId == r.CompanyId
                    && m.ProductId == r.ProductId && m.WarehouseId == r.WarehouseId
                    && (m.CostBasis.HasValue || m.RunningAverageCost > 0m))
                .OrderByDescending(m => m.SequenceNumber)
                .Select(m => m.CostBasis ?? m.RunningAverageCost).Cast<decimal?>().FirstOrDefaultAsync(ct);
        var resolvedUnitCost = basis ?? 0m;
        var resultQty = previousQty + r.Quantity;
        // ERP-PRECISION-OPERATIONAL-05B: el costo promedio corrido se persiste con la escala
        // averageCostDecimals de la política de la empresa (con la columna ya ampliada a numeric(22,10)
        // ya no es la BD quien lo redondea). Fail-closed (05B1): el provider es obligatorio y lanza si
        // no hay contexto de empresa o política — nunca hay fallback silencioso a 6 decimales.
        var precision = await _precision.GetEffectiveAsync(ct);
        var averageCostDecimals = precision.AverageCostDecimals;
        // ACCOUNTING-CONTINUITY-05: el costo de ventas reconocido usa la precisión contable de la
        // empresa (SSOT de precisión), nunca una escala fija.
        var accountingDecimals = precision.AccountingDecimals;
        decimal Money(decimal value) => Math.Round(value, accountingDecimals, MidpointRounding.AwayFromZero);

        // INVENTORY-V1-B3: StockMovement.TotalCost es el único importe monetario del movimiento
        // (AccountingDecimals); el valor corrido es la suma exacta de esos importes. Una reversa o
        // transferencia trae el importe del movimiento original. La salida que vacía la bodega se
        // lleva el valor restante; en una venta que cruza a negativo, el tramo sin stock se valora
        // al costo provisional (mismo importe que su SaleCostObligation).
        var quantity = Math.Abs(r.Quantity);
        decimal? totalCost = r.TotalCost.HasValue ? Money(r.TotalCost.Value)
            : basis.HasValue ? Money(quantity * basis.Value) : null;
        var resolvedSaleCost = 0m;
        if (r.Quantity < 0m && r.TotalCost is null && basis.HasValue)
        {
            var inStock = Math.Min(quantity, Math.Max(0m, previousQty));
            resolvedSaleCost = inStock == previousQty && inStock > 0m ? lastRunningValue : Money(inStock * basis.Value);
            totalCost = resolvedSaleCost + Money((quantity - inStock) * basis.Value);
        }
        var newRunningStockValue = lastRunningValue + (r.Quantity < 0m ? -1m : 1m) * (totalCost ?? 0m);
        var newRunningAverageCost =
            resultQty > 0m
                ? Math.Round(
                    newRunningStockValue / resultQty,
                    averageCostDecimals,
                    MidpointRounding.AwayFromZero
                )
                : resolvedUnitCost;

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
            // TECH-DEBT-API-INVENTORY-ADJUSTMENT-FAILURE-01A: r.UnitCost crudo (nunca resolvedUnitCost)
            // — UnitCost debe reflejar SOLO el costo capturado/manual del caller (no nulo únicamente
            // en entradas). Para una salida (venta/ajuste negativo) el caller nunca pasa unitCost
            // explícito, así que este campo queda null — esa es la semántica que
            // InventoryAdjustmentsEndToEndTests.Escenario3 verifica.
            r.UnitCost,
            r.LotId,
            r.SerialId,
            r.SourceDocLineId,
            // ACCOUNTING-INVENTORY-COGS-07: resolvedUnitCost (costo promedio corrido vigente cuando
            // no hay costo manual) sigue alimentando SOLO TotalCost — vía el parámetro dedicado
            // valuationUnitCost, nunca UnitCost — para que Accounting pueda costear salidas sin que
            // eso se confunda con "esta salida capturó un costo manual".
            valuationUnitCost: basis,
            documentedFreePurchase: await IsDocumentedFreePurchaseAsync(r, ct)
        );

        var pendingCost = basis is null || (r.MovementType == StockMovementType.SaleExit && resultQty < 0m);
        if (r.MovementType == StockMovementType.SaleExit && pendingCost
            && (r.SourceDocId is null || r.SourceDocLineId is null))
            throw new ERP.Domain.Exceptions.DomainRuleViolationException("El costo pendiente requiere documento y línea de venta.");
        if (r.MovementType == StockMovementType.SaleExit && r.SourceDocLineId.HasValue)
        {
            var pendingQuantity = basis is null ? -r.Quantity
                : Math.Min(-r.Quantity, Math.Max(0m, -resultQty));
            _db.Set<SaleCostObligation>().Add(SaleCostObligation.Create(movement, pendingQuantity, basis,
                basis is null ? null : resolvedSaleCost));
        }
        else if (r.MovementType == StockMovementType.SaleReturn && r.SourceDocLineId.HasValue)
        {
            Guid invoiceId;
            Guid invoiceLineId;
            if (r.SourceDocType == "SalesReturn")
            {
                var detail = await _db.Set<ERP.Domain.Modules.Sales.Entities.SalesReturnDetail>()
                    .FirstAsync(d => d.Id == r.SourceDocLineId.Value && d.ReturnId == r.SourceDocId && d.TenantId == r.TenantId, ct);
                var document = await _db.Set<ERP.Domain.Modules.Sales.Entities.SalesReturn>()
                    .FirstAsync(d => d.Id == r.SourceDocId && d.TenantId == r.TenantId && d.CompanyId == r.CompanyId, ct);
                invoiceId = document.SalesInvoiceId;
                invoiceLineId = detail.OriginalInvoiceDetailId;
            }
            else
            {
                invoiceId = r.SourceDocId ?? Guid.Empty;
                invoiceLineId = r.SourceDocLineId.Value;
            }
            var obligations = await GetCostObligationsAsync(r, ct);
            var original = obligations.SingleOrDefault(o => o.InvoiceId == invoiceId && o.InvoiceLineId == invoiceLineId);
            if (original is not null)
            {
                var ledger = new InventoryCostLedger(_db);
                await ledger.LockInvoiceAsync(r.TenantId, r.CompanyId, invoiceId, ct);
                // ACCOUNTING-CONTINUITY-05: el costo reconocido es operativo (valoración del Kardex),
                // independiente de Accounting: costo vigente de la factura redondeado, antes y después.
                var priorRecognizedCost = RoundRecognizedCost(
                    await ledger.GetCurrentInvoiceCostAsync(r.TenantId, r.CompanyId, invoiceId, accountingDecimals, ct),
                    accountingDecimals);
                var allocation = original.Return(movement, r.Quantity, accountingDecimals);
                _db.Set<SaleCostAllocation>().Add(allocation);
                var remainingCost = await ledger.GetCurrentInvoiceCostAsync(r.TenantId, r.CompanyId, invoiceId,
                    accountingDecimals, ct);
                var returnedCost = priorRecognizedCost - RoundRecognizedCost(remainingCost, accountingDecimals);
                if (returnedCost < 0m)
                    throw new ERP.Domain.Exceptions.DomainRuleViolationException("La devolución no tiene una base contable de costo consistente.");
                totalCost = returnedCost == 0m && allocation.PreviousUnitCost is null
                    && allocation.ResolvedQuantity == 0m ? null : returnedCost;
                newRunningStockValue = lastRunningValue + returnedCost;
                // Solo la devolución de venta es un hecho de costo nuevo. La anulación de la factura no
                // publica delta: su costo contable se resuelve con el reverso espejo de lo publicado y la
                // cancelación de lo pendiente (SalesInvoiceCancelledPostingTranslator).
                // El importe contable se resuelve al contabilizar: nuevo costo objetivo tras la
                // devolución − costo ya contabilizado (Posted) de la factura.
                if (r.SourceDocType == "SalesReturn")
                    allocation.RecognizeCost(movement.Id, -returnedCost, r.EffectiveDate,
                        RoundRecognizedCost(remainingCost, accountingDecimals));
                // Resolved returned units are a real-cost entry. They can cover another still-live
                // sale deficit, without reopening the returned original units or changing historical rows.
                if (allocation.ActualUnitCost.HasValue && allocation.ResolvedQuantity > 0m)
                {
                    // The returned inventory carries its allocated recognized monetary value,
                    // including the proportional rounding remainder, not a new commercial price.
                    var returnedUnitCost = allocation.CogsAdjustment != 0m
                        ? returnedCost * allocation.ActualUnitCost.Value / -allocation.CogsAdjustment
                        : allocation.ActualUnitCost.Value;
                    basis = returnedUnitCost;
                    newRunningStockValue -= await CoverPendingCostsAsync(r, movement, allocation.ResolvedQuantity,
                        returnedUnitCost, Money(allocation.ResolvedQuantity * returnedUnitCost), accountingDecimals, ct);
                }
            }
            // Legacy sales have no A2 obligations: their existing return behavior is preserved.
        }
        else if (r.Quantity > 0m && totalCost.HasValue && (r.UnitCost.HasValue || r.TotalCost.HasValue))
        {
            newRunningStockValue -= await CoverPendingCostsAsync(r, movement, r.Quantity,
                r.UnitCost ?? totalCost.Value / r.Quantity, totalCost.Value, accountingDecimals, ct);
        }
        var outstanding = (await GetCostObligationsAsync(r, ct)).Where(o => o.PendingQuantity > 0m).ToList();
        pendingCost = basis is null || outstanding.Count > 0;
        if (resultQty < 0m)
        {
            var valued = outstanding.Where(o => o.ProvisionalUnitCost.HasValue).ToList();
            var valuedQuantity = valued.Sum(o => o.PendingQuantity);
            if (valuedQuantity > 0m)
                basis = valued.Sum(o => o.PendingQuantity * o.ProvisionalUnitCost!.Value) / valuedQuantity;
        }
        // INVENTORY-RUNNING-AVERAGE-PRECISION-01: una salida valuada al promedio corrido (sin costo
        // propio) no cambia el promedio; re-derivarlo del valor ya redondeado a StockValueScale
        // introduciría deriva de redondeo en cada egreso.
        var consumesAtAverage = r.Quantity < 0m && r.UnitCost is null && basis.HasValue;
        if (resultQty <= 0m)
            newRunningAverageCost = basis ?? 0m;
        else if (consumesAtAverage)
            newRunningAverageCost = Math.Round(basis!.Value, averageCostDecimals, MidpointRounding.AwayFromZero);
        else
            newRunningAverageCost = Math.Round(newRunningStockValue / resultQty, averageCostDecimals, MidpointRounding.AwayFromZero);
        if (resultQty > 0m && basis.HasValue) basis = newRunningAverageCost;
        movement.CompleteValuation(basis, pendingCost, newRunningAverageCost, newRunningStockValue, totalCost);

        await _db.Set<StockMovement>().AddAsync(movement, ct);

        // CurrentStock se actualiza como proyección derivada del mismo hecho — no es su origen.
        stock.ApplyKardexMovement(movement, r.AllowNegativeSale, r.EnforceAvailability);

        return movement;
    }

    private async Task<bool> IsDocumentedFreePurchaseAsync(PendingMovement r, CancellationToken ct)
    {
        if (r.MovementType != StockMovementType.PurchaseEntry || r.UnitCost != 0m || r.TotalCost != 0m
            || r.SourceDocType != "PurchaseInvoice" || r.SourceDocId is null || r.SourceDocLineId is null)
            return false;
        // Tracking returns the aggregate already confirmed/frozen by the handler, before its SaveChanges.
        var invoice = await _db.Set<PurchaseInvoice>().Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.TenantId == r.TenantId && i.CompanyId == r.CompanyId
                && i.Id == r.SourceDocId, ct);
        var line = invoice?.Lines.SingleOrDefault(l => l.Id == r.SourceDocLineId);
        if (invoice is null || line?.PurchaseReceptionLineId is null)
            return false;
        var source = await _db.Set<PurchaseReceptionDocument>().Include(d => d.Lines)
            .SingleOrDefaultAsync(d => d.TenantId == r.TenantId && d.CompanyId == r.CompanyId
                && d.Lines.Any(l => l.Id == line.PurchaseReceptionLineId), ct);
        return source is not null && PurchasePromotionCost.IsDocumentedFreeEntry(invoice, line, source,
            r.ProductId, r.WarehouseId, r.Quantity, r.UomCode, r.UnitCost, r.TotalCost);
    }

    private async Task LockStockResourceAsync(PendingMovement r, CancellationToken ct)
    {
        if (!_db.Database.IsNpgsql()) return;
        if (_db.Database.CurrentTransaction is null)
            _ownedStockTransaction = await _db.Database.BeginTransactionAsync(ct);
        await AcquireResourceLockAsync(r.TenantId, r.CompanyId, r.ProductId, r.WarehouseId, ct);
        // An authorization precheck may have loaded an older snapshot before this resource lock.
        foreach (var entry in _db.ChangeTracker.Entries<CurrentStock>().Where(e =>
            e.Entity.TenantId == r.TenantId && e.Entity.CompanyId == r.CompanyId
            && e.Entity.ProductId == r.ProductId && e.Entity.WarehouseId == r.WarehouseId
            && e.State == EntityState.Unchanged).ToList())
            await entry.ReloadAsync(ct);
    }

    public async Task AcquireResourceLockAsync(Guid tenantId, Guid companyId, Guid productId,
        Guid warehouseId, CancellationToken ct = default)
    {
        if (!_company.HasCompanyContext || companyId != _company.CompanyId || tenantId == Guid.Empty)
            throw new ERP.Domain.Exceptions.DomainRuleViolationException("Empresa de inventario inválida.");
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("El lock de inventario requiere una transacción activa.");
        var key = $"inventory:{tenantId}:{companyId}:{productId}:{warehouseId}";
        // pg_advisory_xact_lock es reentrante y se libera con la transacción, incluso tras rollback.
        // No reutilizar un cache de claves entre transacciones del mismo DbContext.
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }

    public Task<bool> HasExitAfterAsync(Guid tenantId, Guid companyId, Guid productId,
        Guid warehouseId, long sequenceNumber, CancellationToken ct = default) =>
        _db.Set<StockMovement>().ForOperationalScope(tenantId, _company)
            .AnyAsync(m => m.CompanyId == companyId && m.ProductId == productId
                && m.WarehouseId == warehouseId && m.SequenceNumber > sequenceNumber
                && m.Quantity < 0m, ct);

    private async Task<List<SaleCostObligation>> GetCostObligationsAsync(PendingMovement r, CancellationToken ct)
    {
        var persisted = await _db.Set<SaleCostObligation>()
            .Where(o => o.TenantId == r.TenantId && o.CompanyId == r.CompanyId
                && o.ProductId == r.ProductId && o.WarehouseId == r.WarehouseId).ToListAsync(ct);
        return persisted.Concat(_db.Set<SaleCostObligation>().Local)
            .Where(o => o.TenantId == r.TenantId && o.CompanyId == r.CompanyId
                && o.ProductId == r.ProductId && o.WarehouseId == r.WarehouseId)
            .DistinctBy(o => o.Id).ToList();
    }

    /// <summary>
    /// Cubre obligaciones pendientes con una entrada de costo real. INVENTORY-V1-B3: cada tramo
    /// cubierto toma su parte monetaria del importe de la entrada (el tramo que la consume entera
    /// absorbe el redondeo) y el ajuste devuelto es el mismo delta del costo vigente de cada
    /// factura que se reconoce en Contabilidad.
    /// </summary>
    private async Task<decimal> CoverPendingCostsAsync(PendingMovement r, StockMovement movement,
        decimal quantity, decimal actualCost, decimal entryCost, int accountingDecimals, CancellationToken ct)
    {
        var adjustment = 0m;
        var obligations = await GetCostObligationsAsync(r, ct);
        foreach (var obligation in obligations.Where(o => o.PendingQuantity > 0m).OrderBy(o => o.SequenceNumber))
        {
            if (quantity <= 0m) break;
            var ledger = new InventoryCostLedger(_db);
            await ledger.LockInvoiceAsync(r.TenantId, r.CompanyId, obligation.InvoiceId, ct);
            var previousCost = RoundRecognizedCost(await ledger.GetCurrentInvoiceCostAsync(
                r.TenantId, r.CompanyId, obligation.InvoiceId, accountingDecimals, ct), accountingDecimals);
            var covered = Math.Min(quantity, obligation.PendingQuantity);
            var coveredCost = covered == quantity ? entryCost
                : Math.Round(covered * actualCost, accountingDecimals, MidpointRounding.AwayFromZero);
            var allocation = obligation.Cover(movement, covered, actualCost, coveredCost);
            _db.Set<SaleCostAllocation>().Add(allocation);
            // El importe contable se resuelve al contabilizar: costo objetivo − costo ya contabilizado
            // (Posted) de la factura; este mismo delta mueve el valor del Kardex.
            var target = RoundRecognizedCost(await ledger.GetCurrentInvoiceCostAsync(
                r.TenantId, r.CompanyId, obligation.InvoiceId, accountingDecimals, ct), accountingDecimals);
            allocation.RecognizeCost(allocation.Id, target - previousCost, r.EffectiveDate, target);
            adjustment += target - previousCost;
            quantity -= covered;
            entryCost -= coveredCost;
        }
        return adjustment;
    }

    private static decimal RoundRecognizedCost(decimal? cost, int accountingDecimals) =>
        Math.Round(cost ?? 0m, accountingDecimals, MidpointRounding.AwayFromZero);

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

        foreach (var entry in _db.ChangeTracker.Entries<SaleCostAllocation>().ToList())
            if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
        foreach (var entry in _db.ChangeTracker.Entries<SaleCostObligation>().ToList())
            if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
            else if (entry.State == EntityState.Modified) await entry.ReloadAsync(ct);

        foreach (var entry in _db.ChangeTracker.Entries<CurrentStock>().ToList())
        {
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
            else if (entry.State == EntityState.Modified)
                await entry.ReloadAsync(ct);
        }

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
        DateTime? toUtcExclusive,
        CancellationToken ct = default
    )
    {
        var from = UtcDateTime.EnsureUtc(fromUtc);
        var to = UtcDateTime.EnsureUtc(toUtcExclusive);
        var q = _db.Set<StockMovement>()
            .ForOperationalScope(tenantId, _company)
            .Where(m => m.ProductId == productId && m.WarehouseId == warehouseId);
        if (from.HasValue)
            q = q.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(m => m.CreatedAt < to.Value);
        return await q.OrderByDescending(m => m.SequenceNumber).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockMovement>> GetMovementsByProductAsync(
        Guid tenantId,
        Guid productId,
        Guid? warehouseId,
        DateTime? fromUtc,
        DateTime? toUtcExclusive,
        CancellationToken ct = default
    )
    {
        var from = UtcDateTime.EnsureUtc(fromUtc);
        var to = UtcDateTime.EnsureUtc(toUtcExclusive);
        var q = _db.Set<StockMovement>()
            .ForOperationalScope(tenantId, _company)
            .Where(m => m.ProductId == productId);
        if (warehouseId.HasValue)
            q = q.Where(m => m.WarehouseId == warehouseId.Value);
        if (from.HasValue)
            q = q.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(m => m.CreatedAt < to.Value);
        return await q.OrderBy(m => m.WarehouseId).ThenBy(m => m.SequenceNumber).ToListAsync(ct);
    }

    public Task<StockMovement?> GetMovementByIdAsync(
        Guid tenantId,
        Guid movementId,
        CancellationToken ct = default
    ) =>
        _db.Set<StockMovement>()
            .ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(m => m.Id == movementId, ct);

    public async Task<IReadOnlyList<StockMovement>> GetMovementsByDocumentAsync(
        Guid tenantId,
        Guid sourceDocId,
        string sourceDocType,
        CancellationToken ct = default
    ) =>
        await _db.Set<StockMovement>()
            .ForOperationalScope(tenantId, _company)
            .Where(m => m.SourceDocId == sourceDocId && m.SourceDocType == sourceDocType)
            .OrderBy(m => m.WarehouseId)
            .ThenBy(m => m.SequenceNumber)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> GetSourceDocIdsWithMovementTypeAsync(
        Guid tenantId,
        string sourceDocType,
        IReadOnlyCollection<Guid> sourceDocIds,
        StockMovementType movementType,
        CancellationToken ct = default
    )
    {
        if (sourceDocIds.Count == 0)
            return new HashSet<Guid>();
        var ids = await _db.Set<StockMovement>()
            .ForOperationalScope(tenantId, _company)
            .Where(m => m.SourceDocType == sourceDocType && m.MovementType == movementType
                && m.SourceDocId != null && sourceDocIds.Contains(m.SourceDocId.Value))
            .Select(m => m.SourceDocId!.Value)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

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
            .ForOperationalScope(tenantId, _company)
            .Where(s => s.ProductId == productId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CurrentStock>> GetForReportAsync(
        Guid tenantId,
        Guid? warehouseId,
        CancellationToken ct = default
    )
    {
        var q = _db.Set<CurrentStock>().ForOperationalScope(tenantId, _company);
        if (warehouseId.HasValue)
            q = q.Where(s => s.WarehouseId == warehouseId.Value);

        return await q.AsNoTracking().ToListAsync(ct);
    }

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
