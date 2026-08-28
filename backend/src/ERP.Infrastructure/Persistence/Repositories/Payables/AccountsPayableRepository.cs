using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Payables;

public sealed class AccountsPayableRepository : IAccountsPayableRepository
{
    private readonly ErpDbContext _db;

    public AccountsPayableRepository(ErpDbContext db) => _db = db;

    public Task<AccountsPayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.AccountsPayables
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .Where(x => x.TenantId == tenantId)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<AccountsPayable?> GetByOriginAsync(
        Guid tenantId,
        Guid companyId,
        AccountsPayableOriginType originType,
        Guid originId,
        CancellationToken ct = default
    ) =>
        _db.AccountsPayables
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .FirstOrDefaultAsync(
                x =>
                    x.TenantId == tenantId
                    && x.CompanyId == companyId
                    && x.OriginType == originType
                    && x.OriginId == originId,
                ct
            );

    public Task<Guid?> GetOriginIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.AccountsPayables
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => (Guid?)x.OriginId)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<AccountsPayable> Items, int Total)> SearchAsync(
        Guid tenantId,
        Guid companyId,
        AccountsPayableOriginType? originType,
        AccountsPayableStatus? status,
        Guid? supplierId,
        DateOnly? dueDateFrom,
        DateOnly? dueDateTo,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = _db.AccountsPayables.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

        if (originType.HasValue)
            q = q.Where(x => x.OriginType == originType.Value);
        if (status.HasValue)
            q = q.Where(x => x.Status == status.Value);
        if (supplierId is not null)
            q = q.Where(x => x.SupplierId == supplierId.Value);
        if (dueDateFrom.HasValue)
            q = q.Where(x => x.Installments.Min(i => i.DueDate) >= dueDateFrom.Value);
        if (dueDateTo.HasValue)
            q = q.Where(x => x.Installments.Min(i => i.DueDate) <= dueDateTo.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            var matchingSupplierIds = _db.BusinessPartners
                .Where(bp =>
                    bp.TenantId == tenantId
                    && (
                        EF.Functions.ILike(bp.Name.LegalName, pattern)
                        || (bp.Name.TradeName != null && EF.Functions.ILike(bp.Name.TradeName, pattern))
                    )
                )
                .Select(bp => bp.Id);
            q = q.Where(x =>
                EF.Functions.ILike(x.DocumentNumber, pattern) || matchingSupplierIds.Contains(x.SupplierId)
            );
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, total);
    }

    public Task AddAsync(AccountsPayable payable, CancellationToken ct = default) =>
        _db.AccountsPayables.AddAsync(payable, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
