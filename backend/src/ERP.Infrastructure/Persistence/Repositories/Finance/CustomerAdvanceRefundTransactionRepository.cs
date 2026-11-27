using ERP.Application.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Finance;

/// <summary>Implementación de <see cref="ICustomerAdvanceRefundTransactionRepository"/> — ADR-052 §4.</summary>
public sealed class CustomerAdvanceRefundTransactionRepository
    : ICustomerAdvanceRefundTransactionRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public CustomerAdvanceRefundTransactionRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<CustomerAdvanceRefundTransaction?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .CustomerAdvanceRefundTransactions.ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CustomerAdvanceRefundTransaction?> GetByMovementIdAsync(
        Guid tenantId,
        Guid movementId,
        CancellationToken ct = default
    ) =>
        _db
            .CustomerAdvanceRefundTransactions.ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(x => x.CustomerAdvanceMovementId == movementId, ct);

    public async Task<IReadOnlyList<CustomerAdvanceRefundTransaction>> ListByAdvanceIdAsync(
        Guid tenantId,
        Guid advanceId,
        CancellationToken ct = default
    ) =>
        await _db
            .CustomerAdvanceRefundTransactions.ForOperationalScope(tenantId, _company)
            .Where(x => x.CustomerAdvanceId == advanceId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public Task AddAsync(
        CustomerAdvanceRefundTransaction transaction,
        CancellationToken ct = default
    ) => _db.CustomerAdvanceRefundTransactions.AddAsync(transaction, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
