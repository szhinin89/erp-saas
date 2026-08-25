using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

/// <summary>Fase 5.5.5.3 — repositorio del agregado <c>Payment</c> (liquidación de AR/AP).</summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-CASH-POSTING-06: proyección liviana (sin Include de líneas) para resolver el
    /// origen documental humano de un JournalEntry — mismo criterio que
    /// <c>ISalesInvoiceRepository.GetJournalSourceSummariesByIdsAsync</c>. <c>Reference</c> es la
    /// única referencia legible que un Payment tiene (no hay numeración documental — no es un
    /// comprobante SRI, ver ADR-026 §7); puede ser null.
    /// </summary>
    Task<
        IReadOnlyDictionary<Guid, (Guid PartnerId, decimal Amount, DateOnly PaymentDate, string? Reference, string Status)>
    > GetJournalSourceSummariesByIdsAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
