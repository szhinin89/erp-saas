using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

public interface ISalesReceivableRepository
{
    Task<SalesReceivable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// ZH-COLLECTIONS-RECEIVABLE-CONCURRENCY-01 — bloquea (SELECT … FOR UPDATE) y recarga las CxC
    /// indicadas, en orden determinista por Id, dentro de la transacción ya abierta por el
    /// llamador. Todo escritor del saldo (cobro, reversa, crédito por devolución) debe leer la
    /// CxC por aquí ANTES de decidir si el monto cabe: el lock serializa las intenciones distintas
    /// que compiten por el mismo saldo. Devuelve solo las CxC del alcance operativo
    /// (tenant + empresa); una ajena queda fuera (fail-closed).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SalesReceivable>> GetByIdsForUpdateAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    /// <summary>Misma garantía que <see cref="GetByIdsForUpdateAsync"/>, por factura de origen.</summary>
    Task<SalesReceivable?> GetByInvoiceIdForUpdateAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct = default
    );
    Task<SalesReceivable?> GetByInvoiceIdAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct = default
    );
    Task<(IReadOnlyList<SalesReceivable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task AddAsync(SalesReceivable receivable, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
