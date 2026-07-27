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
    Task SaveChangesAsync(CancellationToken ct = default);
}
