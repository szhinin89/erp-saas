using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

/// <summary>
/// Contrato de persistencia de <see cref="SupplierCreditRefundTransaction"/> — diseño P0-02 §6.4,
/// Fase 2.
/// </summary>
public interface ISupplierCreditRefundTransactionRepository
{
    Task<SupplierCreditRefundTransaction?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    Task<SupplierCreditRefundTransaction?> GetBySupplierCreditMovementIdAsync(
        Guid tenantId,
        Guid supplierCreditMovementId,
        CancellationToken ct = default
    );

    /// <summary>
    /// P0-02 Fase 8 — bloqueo real <c>SELECT ... FOR SHARE</c> sobre la transacción original
    /// <c>REFUND_RECEIVED</c> (§6.4quater/§6.4quinquies paso 2 de <c>ReverseRefund</c>), adquirido
    /// dentro de la transacción ambiente ya abierta. Se libera automáticamente al COMMIT/ROLLBACK.
    /// </summary>
    Task<SupplierCreditRefundTransaction?> GetByIdForShareAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    Task AddAsync(SupplierCreditRefundTransaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
