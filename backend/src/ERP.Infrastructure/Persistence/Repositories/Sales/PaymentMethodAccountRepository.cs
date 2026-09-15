using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

/// <summary>
/// Implementación de <see cref="IPaymentMethodAccountRepository"/> —
/// SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01.
/// </summary>
public sealed class PaymentMethodAccountRepository : IPaymentMethodAccountRepository
{
    private readonly ErpDbContext _db;

    public PaymentMethodAccountRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task<PaymentMethodAccount?> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid paymentMethodId,
        CancellationToken ct = default
    ) =>
        _db.PaymentMethodAccounts.FirstOrDefaultAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.PaymentMethodId == paymentMethodId,
            ct
        );

    public async Task<IReadOnlyDictionary<Guid, PaymentMethodAccount>> GetMapAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    )
    {
        var rows = await _db
            .PaymentMethodAccounts.Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.PaymentMethodId);
    }

    public async Task<IReadOnlyList<PaymentMethodAccount>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    ) =>
        await _db
            .PaymentMethodAccounts.Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .ToListAsync(ct);

    public Task AddAsync(PaymentMethodAccount entity, CancellationToken ct = default) =>
        _db.PaymentMethodAccounts.AddAsync(entity, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
