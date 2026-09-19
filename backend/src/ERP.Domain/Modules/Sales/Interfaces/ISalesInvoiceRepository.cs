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

    /// <summary>
    /// CASH-SESSION-COLLECTION-SUMMARY-01 — proyección plana (una fila por pago) de las facturas
    /// AUTORIZADAS de una sesión de caja, para armar el resumen de cobros del turno sin cargar el
    /// agregado completo (líneas/impuestos) ni traer Draft/Cancelled. Una factura con N formas de
    /// pago produce N filas con el mismo InvoiceId/InvoiceNumber/GrandTotal — el handler agrupa por
    /// InvoiceId para "facturas distintas" y por PaymentMethodId para el desglose por forma.
    /// </summary>
    Task<IReadOnlyList<SalesInvoiceCashSessionPaymentRow>> GetCollectionSummaryByCashSessionAsync(
        Guid tenantId,
        Guid branchId,
        Guid cashSessionId,
        CancellationToken ct = default
    );

    /// <summary>
    /// CASH-SESSION-LIST-SUMMARY-01 — misma proyección que
    /// <see cref="GetCollectionSummaryByCashSessionAsync"/> pero para VARIAS sesiones a la vez (una
    /// página del listado de turnos): un solo query para toda la página en vez de N+1 por fila.
    /// El handler agrupa por <see cref="SalesInvoiceCashSessionPaymentRow.CashSessionId"/>.
    /// </summary>
    Task<IReadOnlyList<SalesInvoiceCashSessionPaymentRow>> GetCollectionSummaryByCashSessionsAsync(
        Guid tenantId,
        Guid branchId,
        IReadOnlyCollection<Guid> cashSessionIds,
        CancellationToken ct = default
    );

    Task AddAsync(SalesInvoice invoice, CancellationToken ct = default);
    Task RemoveLinesByInvoiceAsync(
        Guid invoiceId,
        IEnumerable<SalesInvoiceDetail> newLines,
        CancellationToken ct = default
    );
    Task RemovePaymentsByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// ADR-033, Fase 4 — borra por SQL directo las filas de cronograma existentes y desengancha su
    /// tracking, para permitir que GeneratePaymentSchedule/ReplacePaymentSchedule reconstruyan la
    /// colección sin conflictos de EF ChangeTracker (mismo patrón que RemovePaymentsByInvoiceAsync).
    /// Llamar ANTES de invocar esos métodos de dominio cuando se está regenerando/reemplazando el
    /// cronograma de un borrador ya persistido (Update). No es necesario en Create (entidad nueva).
    /// </summary>
    Task RemovePaymentSchedulesByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// CASH-SESSION-COLLECTION-SUMMARY-01/UX-02 — una fila por <c>SalesInvoicePayment</c> de una
/// factura autorizada del turno. <see cref="PaymentMethodName"/> es el snapshot ya guardado en el
/// pago (<c>SalesInvoicePayment.PaymentMethodName</c>) — no requiere join con el catálogo vivo de
/// <c>PaymentMethod</c> para mostrar el nombre; el handler solo une contra el catálogo para
/// resolver <c>IsCreditAllowed</c>/<c>DetailType</c> (datos que el snapshot del pago no guarda).
/// Los campos <c>Transfer*</c> vienen del owned type <c>PaymentTransferDetail</c> (incluido
/// automáticamente por EF, sin join adicional) — todos null cuando el pago no es Transferencia.
/// </summary>
public sealed record SalesInvoiceCashSessionPaymentRow(
    Guid CashSessionId,
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime AuthorizedAt,
    string CustomerName,
    decimal GrandTotal,
    Guid PaymentMethodId,
    string PaymentMethodCode,
    string PaymentMethodName,
    decimal Amount,
    /// <summary>Referencia/comprobante genérico capturado en el pago (<c>SalesInvoicePayment.Reference</c>) — aplica a cualquier forma de pago.</summary>
    string? Reference,
    /// <summary>Solo Transferencia — cuenta bancaria destino real (SALES-TRANSFER-BANK-ACCOUNT-01). Null en pagos anteriores a ese ticket o en otras formas de pago.</summary>
    Guid? TransferCompanyBankAccountId,
    /// <summary>Solo Transferencia legacy — texto libre de banco anterior a <see cref="TransferCompanyBankAccountId"/>. Nunca escrito por código nuevo.</summary>
    string? TransferLegacyBankName,
    /// <summary>Solo Transferencia — número de comprobante de la operación bancaria.</summary>
    string? TransferReceiptNumber,
    /// <summary>Solo Transferencia — fecha de la operación bancaria.</summary>
    DateOnly? TransferDate
);
