using ERP.Domain.Modules.Purchases.Entities;

namespace ERP.Domain.Modules.Purchases.Interfaces;

/// <summary>
/// Contrato de persistencia de <see cref="PurchaseReturn"/> — diseño P0-02 §7.1, Fase 2.
/// </summary>
public interface IPurchaseReturnRepository
{
    Task<PurchaseReturn?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Advisory lock transaccional por <c>(TenantId, PurchaseInvoiceId)</c> — Lock A del diseño
    /// (§15.1, namespace <c>"PurchaseInvoice.FinancialLock"</c>), serializa toda operación
    /// financiera concurrente sobre la misma factura de compra sin bloquear otras facturas. Se
    /// libera automáticamente al COMMIT/ROLLBACK de la transacción ambiente; nunca abre ni
    /// comitea una transacción propia.
    /// </summary>
    Task AcquireFinancialLockAsync(
        Guid tenantId,
        Guid purchaseInvoiceId,
        CancellationToken ct = default
    );

    Task AddAsync(PurchaseReturn purchaseReturn, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// PI-CANC-01 (diseño §5.1 caso 1, §21) — indica si existe una <see cref="PurchaseReturn"/> en
    /// estado <c>Authorized</c> asociada a <paramref name="purchaseInvoiceId"/>. Consulta de solo
    /// lectura, invocada bajo Lock A ya adquirido por el caller — no adquiere locks ni abre
    /// transacción propia.
    /// </summary>
    Task<bool> ExistsAuthorizedByPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid purchaseInvoiceId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// P0-02 Fase 5 — mecanismo de idempotencia obligatorio de <c>CreateDraft</c> (diseño §16.2,
    /// §16.2bis): localiza por la clave única <c>(TenantId, CreateClientRequestId)</c> la
    /// devolución ya creada por una solicitud idéntica o en conflicto, antes de insertar una
    /// nueva. Incluye <c>Lines</c> para poder reconstruir el snapshot ya confirmado sin una
    /// segunda consulta.
    /// </summary>
    Task<PurchaseReturn?> GetByCreateClientRequestIdAsync(
        Guid tenantId,
        Guid createClientRequestId,
        CancellationToken ct = default
    );

    /// <summary>
    /// P0-02 Fase 5 — cantidad ya devuelta (§10.2) por línea de factura original: suma de
    /// <c>PurchaseReturnDetail.Quantity</c> agrupada por <c>OriginalInvoiceDetailId</c>,
    /// restringida a devoluciones en estado <see cref="Enums.PurchaseReturnStatus.Authorized"/> —
    /// consulta derivada, sin contador mutable. Claves ausentes en el resultado implican
    /// remanente igual a la cantidad original completa.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetReturnedQuantitiesByInvoiceDetailIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> invoiceDetailIds,
        CancellationToken ct = default
    );

    /// <summary>P0-02 Fase 5 — listado paginado para <c>GetPurchaseReturnListQuery</c>, mismo patrón que <c>IPurchaseInvoiceRepository.GetPagedAsync</c>.</summary>
    Task<(IReadOnlyList<PurchaseReturn> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// P0-02 Fase 6 — descubrimiento mínimo, sin tracking, del <c>PurchaseInvoiceId</c> dueño de
    /// un <c>PurchaseReturn</c>, usado únicamente para determinar qué Lock A adquirir ANTES de la
    /// recarga autoritativa en <c>AuthorizePurchaseReturnHandler</c> — mismo patrón exacto que
    /// <c>IPurchasePayableRepository.GetPurchaseInvoiceIdAsync</c> (Fase 3, Remediación
    /// Transaccional 02). Deliberadamente no rastrea la entidad — así la posterior llamada a
    /// <see cref="GetByIdAsync"/> (ya tracking) ejecutada después del lock garantiza una lectura
    /// fresca real desde PostgreSQL, nunca la misma instancia servida por el identity map de EF Core.
    /// </summary>
    Task<Guid?> GetPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid purchaseReturnId,
        CancellationToken ct = default
    );
}
