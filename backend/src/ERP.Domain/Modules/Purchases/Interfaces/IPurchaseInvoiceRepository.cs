using ERP.Domain.Modules.Purchases.Entities;

namespace ERP.Domain.Modules.Purchases.Interfaces;

public interface IPurchaseInvoiceRepository
{
    Task<PurchaseInvoice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<PurchaseInvoice?> GetByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    );

    /// <summary>
    /// <c>LineCounts</c> se resuelve con un COUNT agrupado, no con <c>Include(Lines)</c> —
    /// el listado no necesita las líneas completas de cada compra, solo su cantidad.
    /// </summary>
    Task<(
        IReadOnlyList<PurchaseInvoice> Items,
        IReadOnlyDictionary<Guid, int> LineCounts,
        int Total
    )> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Compras emitidas en el rango de fechas indicado (inclusive), opcionalmente acotadas a un
    /// proveedor, con líneas cargadas para que los totales calculados (Subtotal/TotalVat/
    /// TotalDiscount/GrandTotal) resuelvan correctamente incluso para compras aún no confirmadas.
    /// Usado por el reporte básico de Compras por proveedor — no pagina, pensado para rangos
    /// acotados.
    /// </summary>
    Task<IReadOnlyList<PurchaseInvoice>> GetForSupplierReportAsync(
        Guid tenantId,
        DateOnly dateFrom,
        DateOnly dateTo,
        Guid? supplierId,
        CancellationToken ct = default
    );

    Task AddAsync(PurchaseInvoice invoice, CancellationToken ct = default);
    Task RemoveLinesByInvoiceAsync(
        Guid invoiceId,
        IEnumerable<PurchaseInvoiceDetail> newLines,
        CancellationToken ct = default
    );
    Task ClearScheduleTrackingAsync(Guid invoiceId, CancellationToken ct = default);
    void ReattachSchedulesAsAdded(PurchaseInvoice invoice);
    Task<PurchasePayable?> GetPayableByPurchaseIdAsync(
        Guid tenantId,
        Guid purchaseId,
        CancellationToken ct = default
    );
    Task<IssuedWithholding?> GetWithholdingByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );
    Task<IssuedWithholding?> GetWithholdingByPurchaseIdAsync(
        Guid tenantId,
        Guid purchaseId,
        CancellationToken ct = default
    );

    /// <summary>
    /// P0-02 Fase 3 (Remediación transaccional 02) — descubrimiento mínimo, sin tracking, del
    /// <c>PurchaseInvoiceId</c> dueño de un <c>IssuedWithholding</c>, usado únicamente para
    /// determinar qué Lock A adquirir ANTES de la recarga autoritativa. Deliberadamente no rastrea
    /// la entidad — así la posterior llamada a <see cref="GetWithholdingByIdAsync"/> (ya tracking)
    /// ejecutada después del lock garantiza una lectura fresca real desde PostgreSQL, nunca la
    /// misma instancia servida por el identity map de EF Core.
    /// </summary>
    Task<Guid?> GetWithholdingPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid withholdingId,
        CancellationToken ct = default
    );
    void TrackPayable(PurchasePayable payable);
    void TrackCommunication(PurchaseCommunication communication);
    void TrackWithholding(IssuedWithholding withholding);
    Task SaveChangesAsync(CancellationToken ct = default);
}
