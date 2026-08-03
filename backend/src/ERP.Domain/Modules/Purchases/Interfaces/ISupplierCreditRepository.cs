using ERP.Domain.Modules.Purchases.Entities;

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

    /// <summary>P0-02 Fase 11 — listado paginado para <c>GetSupplierCreditListQuery</c>, mismo patrón que <c>IPurchaseReturnRepository.GetPagedAsync</c>.</summary>
    Task<(IReadOnlyList<SupplierCredit> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
}
