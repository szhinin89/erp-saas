using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;

namespace ERP.Domain.Modules.Purchases.Interfaces;

/// <summary>
/// Contrato de persistencia de <see cref="SupplierCredit"/> — diseño P0-02 §7.4, Fase 2.
/// </summary>
public interface ISupplierCreditRepository
{
    Task<SupplierCredit?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<SupplierCredit?> GetBySourcePurchaseReturnIdAsync(
        Guid tenantId,
        Guid sourcePurchaseReturnId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Advisory lock transaccional por <c>(TenantId, SupplierCreditId)</c> — Lock B del diseño
    /// (§15.1, namespace <c>"SupplierCredit.Lock"</c>), adquirido siempre después de Lock A
    /// cuando ambos participan en la misma operación (§15.4). Se libera automáticamente al
    /// COMMIT/ROLLBACK de la transacción ambiente; nunca abre ni comitea una transacción propia.
    /// </summary>
    Task AcquireLockAsync(Guid tenantId, Guid supplierCreditId, CancellationToken ct = default);

    Task AddAsync(SupplierCredit supplierCredit, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// P0-02 Fase 10 — descubrimiento mínimo, sin tracking, del <c>SupplierCredit.Id</c> (si
    /// existe) originado por un <c>PurchaseReturn</c>, usado únicamente para determinar si hay
    /// que adquirir Lock B (§15.4) ANTES de la recarga autoritativa en
    /// <c>CancelPurchaseReturnHandler</c> — mismo patrón exacto que
    /// <c>IPurchaseReturnRepository.GetPurchaseInvoiceIdAsync</c>. Deliberadamente no rastrea la
    /// entidad — así la posterior llamada a <see cref="GetByIdAsync"/> (ya tracking) ejecutada
    /// después del lock garantiza una lectura fresca real desde PostgreSQL, nunca la misma
    /// instancia servida por el identity map de EF Core.
    /// </summary>
    Task<Guid?> GetIdBySourcePurchaseReturnIdAsync(
        Guid tenantId,
        Guid sourcePurchaseReturnId,
        CancellationToken ct = default
    );

    /// <summary>
    /// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — mismo patrón exacto que
    /// <see cref="GetIdBySourcePurchaseReturnIdAsync"/> (descubrimiento sin tracking antes de Lock
    /// B), para el anticipo originado por un <c>SupplierPayment</c>.
    /// </summary>
    Task<Guid?> GetIdBySourceSupplierPaymentIdAsync(
        Guid tenantId,
        Guid sourceSupplierPaymentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — documento de origen de cada crédito (devolución:
    /// número + instante de autorización; pago: SystemNumber + PaymentDate), solo lectura, en DOS
    /// consultas fijas para todo el lote (nunca una por crédito). Créditos cuyo origen no puede
    /// resolverse no aparecen en el diccionario. Reemplaza <c>GetSourceDocumentNumbersAsync</c> (02C).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SupplierCreditSourceDocument>> GetSourceDocumentsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> supplierCreditIds,
        CancellationToken ct = default
    );

    /// <summary>
    /// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — listado paginado con filtros ejecutados en BD (empresa
    /// operativa fail-closed), sin cargar movimientos, orden estable (CreatedAt desc, Id desc).
    /// Reemplaza <c>GetPagedAsync</c> (Fase 11).
    /// </summary>
    Task<(IReadOnlyList<SupplierCredit> Items, int Total)> SearchAsync(
        Guid tenantId,
        SupplierCreditSearchCriteria criteria,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
}

/// <summary>ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — filtros del listado de saldos a favor (todos opcionales).</summary>
public sealed record SupplierCreditSearchCriteria(
    Guid? SupplierId = null,
    SupplierCreditSourceType? SourceType = null,
    bool? IsOpen = null
);

/// <summary>
/// ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — documento de origen tal como lo guarda el dominio:
/// <see cref="BusinessDate"/> para el pago (<c>PaymentDate</c>), <see cref="AuthorizedAtUtc"/> para
/// la devolución (que no tiene fecha de negocio propia; Application la convierte a fecha de la
/// empresa con su zona horaria, ADR-034).
/// </summary>
public sealed record SupplierCreditSourceDocument(
    Guid DocumentId,
    string? Number,
    DateOnly? BusinessDate,
    DateTime? AuthorizedAtUtc
);
