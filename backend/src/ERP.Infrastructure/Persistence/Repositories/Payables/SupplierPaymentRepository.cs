using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Payables;

public sealed class SupplierPaymentRepository : ISupplierPaymentRepository
{
    private readonly ErpDbContext _db;

    public SupplierPaymentRepository(ErpDbContext db) => _db = db;

    public Task<SupplierPayment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.SupplierPayments
            .Include(x => x.MethodLines)
            .Include(x => x.ApplicationLines)
            .Include(x => x.AllocationLines)
            .Where(x => x.TenantId == tenantId)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<SupplierPayment> Items, int Total)> SearchAsync(
        Guid tenantId,
        Guid companyId,
        Guid? supplierId,
        SupplierPaymentStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = _db.SupplierPayments.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

        if (supplierId is not null)
            q = q.Where(x => x.SupplierId == supplierId.Value);
        if (status.HasValue)
            q = q.Where(x => x.Status == status.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.MethodLines)
            .Include(x => x.ApplicationLines)
            .Include(x => x.AllocationLines)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> ExistsByReceiptNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid supplierId,
        string receiptNumber,
        CancellationToken ct = default
    ) =>
        _db.SupplierPayments.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.SupplierId == supplierId
                && x.ReceiptNumber == receiptNumber,
            ct
        );

    public Task AddAsync(SupplierPayment payment, CancellationToken ct = default) =>
        _db.SupplierPayments.AddAsync(payment, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
