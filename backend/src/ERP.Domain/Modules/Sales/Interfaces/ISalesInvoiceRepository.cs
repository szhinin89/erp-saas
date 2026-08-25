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

    /// <summary>
    /// Proyección para el listado de Cuentas por Cobrar (FINANCE-RECEIVABLES-LIST-ENTERPRISE-01):
    /// datos de la factura origen que <c>SalesReceivable</c> no tiene por sí sola (número,
    /// cliente, sucursal, usuario que facturó, fecha de emisión) — un solo query por página en
    /// vez de N+1 por fila de la grilla.
    /// </summary>
    Task<
        IReadOnlyDictionary<
            Guid,
            (
                string InvoiceNumber,
                string CustomerName,
                string CustomerTaxId,
                string CustomerIdentificationType,
                Guid BranchId,
                Guid CreatedBy,
                DateOnly IssueDate,
                DateTime CreatedAt
            )
        >
    > GetReceivableSummariesByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-SOURCE-TRACEABILITY-04: proyección liviana para resolver el origen documental
    /// humano de un JournalEntry (número, cliente, estado, fecha) — mismo criterio que
    /// <see cref="GetSummariesByIdsAsync"/> (consumidor externo al módulo, sin cargar el
    /// agregado completo), agregada aparte para no romper ese contrato ya usado por el Monitor
    /// de Documentos Electrónicos.
    /// </summary>
    Task<
        IReadOnlyDictionary<
            Guid,
            (string InvoiceNumber, string CustomerName, string Status, DateOnly IssueDate)
        >
    > GetJournalSourceSummariesByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Facturas emitidas en el rango de fechas indicado (inclusive), con líneas cargadas para
    /// que los totales calculados (Subtotal/TotalVat/TotalDiscount/GrandTotal) resuelvan
    /// correctamente incluso para facturas aún no autorizadas. Usado por el reporte básico de
    /// Ventas del día — no pagina, pensado para rangos acotados (día/semana).
    /// </summary>
    Task<IReadOnlyList<SalesInvoice>> GetForDailyReportAsync(
        Guid tenantId,
        DateOnly dateFrom,
        DateOnly dateTo,
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
