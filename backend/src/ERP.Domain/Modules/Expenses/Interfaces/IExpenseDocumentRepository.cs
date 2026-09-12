using ERP.Domain.Modules.Expenses.Entities;

namespace ERP.Domain.Modules.Expenses.Interfaces;

public interface IExpenseDocumentRepository
{
    Task<(IReadOnlyList<ExpenseDocument> Items, IReadOnlyDictionary<Guid, int> LineCounts, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid branchId,
        string? search = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 25,
        CancellationToken ct = default
    );

    Task<ExpenseDocument?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — solo cuenta como duplicado un gasto ACTIVO
    /// (Draft/Confirmed) con este supplier+tipo+número; un gasto <c>Cancelled</c> es historial,
    /// nunca bloquea reutilizar el mismo número.
    /// </summary>
    Task<ExpenseDocument?> GetBySupplierAndDocumentNumberAsync(
        Guid tenantId,
        Guid supplierId,
        string documentType,
        string documentNumber,
        CancellationToken ct = default
    );

    /// <summary>
    /// EXPENSES-FROM-RECEPTION-01 — usado para impedir que la misma factura de recepción termine
    /// registrada como Gasto más de una vez, y como mitad del chequeo cruzado Compra↔Gasto (la
    /// otra mitad es <c>IPurchaseInvoiceRepository.GetByAccessKeyAsync</c>).
    /// RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — solo cuenta un gasto ACTIVO; uno
    /// <c>Cancelled</c> nunca bloquea.
    /// </summary>
    Task<bool> ExistsByAccessKeyAsync(Guid tenantId, string accessKey, CancellationToken ct = default);

    /// <summary>
    /// PURCHASE-RECEPTION-BULK-SRI-XML-DOWNLOAD-01 — Id del gasto ACTIVO (Draft/Confirmed) con
    /// este AccessKey, si existe (nunca uno <c>Cancelled</c>) — mismo criterio que
    /// <see cref="ExistsByAccessKeyAsync"/>, pero devolviendo el Id en vez de solo un booleano,
    /// para refrescar la fila de Recepción sin abrir el documento.
    /// </summary>
    Task<Guid?> GetActiveIdByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    );

    /// <summary>
    /// RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — solo cuenta un gasto ACTIVO vinculado a esta
    /// recepción; uno <c>Cancelled</c> nunca bloquea reprocesar la misma recepción.
    /// </summary>
    Task<bool> ExistsByReceptionDocumentIdAsync(Guid tenantId, Guid receptionDocumentId, CancellationToken ct = default);

    /// <summary>
    /// RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — Id del gasto Cancelled más reciente con este
    /// AccessKey, si existe (nunca el activo) — para "Ver gasto anulado" (historial) en la UI de
    /// Recepción.
    /// </summary>
    Task<Guid?> GetLatestCancelledIdByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    );

    Task AddAsync(ExpenseDocument document, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
