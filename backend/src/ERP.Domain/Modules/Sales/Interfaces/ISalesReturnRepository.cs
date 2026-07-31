using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

public interface ISalesReturnRepository
{
    Task<SalesReturn?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<SalesReturn> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Suma la cantidad ya devuelta contra una línea de factura, considerando exclusivamente
    /// devoluciones en estado <c>Authorized</c> (excluye <c>Draft</c>/<c>Cancelled</c>) — usada
    /// por Application para calcular el remanente devolvible antes de autorizar una nueva
    /// devolución.
    /// </summary>
    Task<decimal> GetReturnedQuantityByInvoiceDetailAsync(
        Guid tenantId,
        Guid invoiceDetailId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Advisory lock transaccional por <c>(TenantId, SalesInvoiceId)</c> — serializa todas las
    /// devoluciones sobre la misma factura sin bloquear devoluciones de otras facturas. Se libera
    /// automáticamente al COMMIT/ROLLBACK de la transacción ambiente; nunca abre ni comitea una
    /// transacción propia. Mismo patrón que
    /// <c>IJournalEntryRepository.AcquireIdempotencyLockAsync</c> (Accounting), con un namespace
    /// de hash independiente para no colisionar con esas claves.
    /// </summary>
    Task AcquireReturnLockAsync(
        Guid tenantId,
        Guid salesInvoiceId,
        CancellationToken ct = default
    );

    Task AddAsync(SalesReturn salesReturn, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
