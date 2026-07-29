using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

public interface ISalesInvoiceRepository
{
    Task<SalesInvoice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<SalesInvoice> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Proyección liviana (sin Include de líneas/pagos) para consumidores de solo lectura
    /// externos al módulo de Ventas — p.ej. el Monitor de Documentos Electrónicos, que solo
    /// necesita mostrar número de factura y nombre de cliente, nunca el agregado completo.
    /// </summary>
    Task<
        IReadOnlyDictionary<Guid, (string InvoiceNumber, string CustomerName)>
    > GetSummariesByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    Task AddAsync(SalesInvoice invoice, CancellationToken ct = default);
    Task RemoveLinesByInvoiceAsync(
        Guid invoiceId,
        IEnumerable<SalesInvoiceDetail> newLines,
        CancellationToken ct = default
    );
    Task RemovePaymentsByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
