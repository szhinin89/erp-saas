using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

/// <summary>Fase 5.5.5.3 — repositorio del agregado <c>Payment</c> (liquidación de AR/AP).</summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
