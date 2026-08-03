using ERP.Domain.Modules.Purchases.Entities;

namespace ERP.Domain.Modules.Purchases.Interfaces;

/// <summary>
/// Fase 5.5.5.3 — carga de <c>PurchasePayable</c> por su propio Id, necesaria para aplicar/reversar
/// pagos (líneas de <c>Payment</c> referencian <c>PurchasePayable.Id</c>, no <c>PurchaseId</c>).
/// Interfaz nueva y separada de <c>IPurchaseInvoiceRepository</c> — esta última ya tiene
/// implementación concreta en Infrastructure y no se modifica en esta fase (Domain-only).
/// </summary>
public interface IPurchasePayableRepository
{
    Task<PurchasePayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — listado paginado necesario para que el usuario
    /// pueda consultar/seleccionar qué cuenta por pagar liquidar antes de registrar un pago.
    /// Mismo patrón exacto que <c>ISalesReceivableRepository.GetPagedAsync</c>.
    /// </summary>
    Task<(IReadOnlyList<PurchasePayable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// P0-02 Fase 13 — filtro adicional opcional por <c>SupplierId</c>, requerido por el selector
    /// de CxP destino de <c>ApplySupplierCreditModal</c> (diseño Fase 13, cambio exacto #2:
    /// "filtrado server-side, nunca client-side sobre una lista completa"). Extensión aditiva —
    /// sobrecarga nueva, no reemplaza <see cref="GetPagedAsync"/>.
    /// </summary>
    Task<(IReadOnlyList<PurchasePayable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        Guid? supplierId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// P0-02 Fase 3 (Remediación transaccional 02) — descubrimiento mínimo, sin tracking, del
    /// <c>PurchaseInvoice.Id</c> (<c>PurchasePayable.PurchaseId</c>) dueño de un <c>PurchasePayable</c>,
    /// usado únicamente para determinar qué Lock A adquirir ANTES de la recarga autoritativa.
    /// Deliberadamente no rastrea la entidad — así la posterior llamada a <see cref="GetByIdAsync"/>
    /// (ya tracking) ejecutada después del lock garantiza una lectura fresca real desde PostgreSQL,
    /// nunca la misma instancia servida por el identity map de EF Core.
    /// </summary>
    Task<Guid?> GetPurchaseInvoiceIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
