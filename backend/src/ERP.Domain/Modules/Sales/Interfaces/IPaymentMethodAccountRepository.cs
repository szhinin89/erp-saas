using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

/// <summary>
/// Contrato de persistencia de <see cref="PaymentMethodAccount"/> — SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01.
/// </summary>
public interface IPaymentMethodAccountRepository
{
    Task<PaymentMethodAccount?> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid paymentMethodId,
        CancellationToken ct = default
    );

    /// <summary>Mapa completo (PaymentMethodId → PaymentMethodAccount) para la Company activa — usado por
    /// <c>AuthorizeSalesInvoiceHandler</c> para resolver todos los métodos de una factura en una sola consulta.</summary>
    Task<IReadOnlyDictionary<Guid, PaymentMethodAccount>> GetMapAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<PaymentMethodAccount>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    );

    Task AddAsync(PaymentMethodAccount entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
