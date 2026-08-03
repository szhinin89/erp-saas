using ERP.Application.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Finance;

/// <summary>Implementación de <see cref="ISupplierCreditRefundTransactionRepository"/> — diseño P0-02 §6.4, Fase 2.</summary>
public sealed class SupplierCreditRefundTransactionRepository
    : ISupplierCreditRefundTransactionRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public SupplierCreditRefundTransactionRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<SupplierCreditRefundTransaction?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .SupplierCreditRefundTransactions.ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SupplierCreditRefundTransaction?> GetBySupplierCreditMovementIdAsync(
        Guid tenantId,
        Guid supplierCreditMovementId,
        CancellationToken ct = default
    ) =>
        _db
            .SupplierCreditRefundTransactions.ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(x => x.SupplierCreditMovementId == supplierCreditMovementId, ct);

    public async Task<SupplierCreditRefundTransaction?> GetByIdForShareAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    )
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM supplier_credit_refund_transactions WHERE id = {id} AND tenant_id = {tenantId} FOR SHARE",
            ct
        );
        return await GetByIdAsync(tenantId, id, ct);
    }

    public Task AddAsync(
        SupplierCreditRefundTransaction transaction,
        CancellationToken ct = default
    ) => _db.SupplierCreditRefundTransactions.AddAsync(transaction, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
