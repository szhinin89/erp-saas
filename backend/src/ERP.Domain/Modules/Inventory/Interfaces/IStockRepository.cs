using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockRepository
{
    Task<CurrentStock?> GetStockAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CurrentStock>> GetStockByWarehouseAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid? productId,
        CancellationToken cancellationToken = default
    );

    Task AddCurrentStockAsync(CurrentStock entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Único punto de entrada autorizado para registrar un hecho de inventario en el Kardex.
    /// Calcula <see cref="StockMovement.SequenceNumber"/>, <see cref="StockMovement.RunningAverageCost"/>
    /// y <see cref="StockMovement.RunningStockValue"/> leyendo exclusivamente el historial de
    /// <see cref="StockMovement"/> (nunca <see cref="CurrentStock"/>), y actualiza la proyección
    /// <see cref="CurrentStock"/> como efecto colateral dentro de la misma unidad de trabajo.
    /// No llama a SaveChanges — eso ocurre una única vez vía <see cref="SaveChangesWithSequenceRetryAsync"/>.
    /// </summary>
    Task<StockMovement> AppendMovementAsync(
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
        CancellationToken cancellationToken = default,
        // P0-02 Fase 6 — trazabilidad línea-a-línea (diseño §10.3/§14.1): referencia genérica a la
        // línea del documento origen (p. ej. PurchaseReturnDetail.Id). Agregado DESPUÉS de
        // cancellationToken (no antes) — muchos call sites existentes pasan cancellationToken
        // posicionalmente como último argumento; insertarlo antes rompería esa posición. Aditivo
        // puro, ningún call site existente lo requiere.
        Guid? sourceDocLineId = null
    );

    /// <summary>
    /// Persiste la unidad de trabajo completa (incluye cualquier otra entidad ya trackeada por
    /// otros repositorios sobre el mismo DbContext). Si dos transacciones concurrentes calcularon
    /// el mismo SequenceNumber para la misma clave (Company/Product/Warehouse), reintenta
    /// recalculando desde el estado real de la base de datos. Los handlers de negocio no necesitan
    /// conocer ni manejar esta lógica de concurrencia.
    /// </summary>
    Task<int> SaveChangesWithSequenceRetryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> GetMovementsAsync(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Kardex de un producto. Si <paramref name="warehouseId"/> es null, retorna el
    /// historial en todas las bodegas (cada fila conserva su propio saldo/costo corrido
    /// por bodega — el Kardex nunca mezcla saldos entre bodegas).
    /// </summary>
    Task<IReadOnlyList<StockMovement>> GetMovementsByProductAsync(
        Guid tenantId,
        Guid productId,
        Guid? warehouseId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default
    );

    Task<StockMovement?> GetMovementByIdAsync(
        Guid tenantId,
        Guid movementId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Búsqueda exacta por documento origen (SourceDocId + SourceDocType). El número de
    /// documento se resuelve siempre en el dominio propietario (Purchases/Sales/...) —
    /// este método nunca matchea por texto libre.
    /// </summary>
    Task<IReadOnlyList<StockMovement>> GetMovementsByDocumentAsync(
        Guid tenantId,
        Guid sourceDocId,
        string sourceDocType,
        CancellationToken cancellationToken = default
    );

    /// <summary>Vecino anterior en la secuencia del Kardex para la misma clave Company/Product/Warehouse.</summary>
    Task<StockMovement?> GetPreviousMovementAsync(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        long sequenceNumber,
        CancellationToken cancellationToken = default
    );

    /// <summary>Vecino siguiente en la secuencia del Kardex para la misma clave Company/Product/Warehouse.</summary>
    Task<StockMovement?> GetNextMovementAsync(
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        long sequenceNumber,
        CancellationToken cancellationToken = default
    );

    Task<(decimal TotalQuantity, decimal TotalStockValue)> GetAggregatedStockAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CurrentStock>> GetStockByProductAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default
    );

    Task<decimal?> GetLastPurchaseCostAsync(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken = default
    );
}
