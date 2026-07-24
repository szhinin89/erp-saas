using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Caja;

public sealed class CashRegisterRepository : ICashRegisterRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public CashRegisterRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<CashRegister> Scoped(Guid tenantId)
        => _db.CashRegisters.ForOperationalScope(tenantId, _company)
            .Include(x => x.Branch)
            .Include(x => x.EmissionPoint).ThenInclude(ep => ep!.Establishment)
            .Include(x => x.DefaultWarehouse)
            .Include(x => x.DefaultCustomer);

    public Task<CashRegister?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Scoped(tenantId).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CashRegister>> GetByBranchAsync(
        Guid tenantId, Guid branchId, bool? activeFilter, CancellationToken ct = default)
    {
        var q = Scoped(tenantId).Where(x => x.BranchId == branchId);

        if (activeFilter.HasValue)
            q = q.Where(x => x.IsActive == activeFilter.Value);

        return await q.OrderBy(x => x.Code).AsNoTracking().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CashRegister>> GetAllByCompanyAsync(
        Guid tenantId, Guid companyId, bool? activeFilter, string? search, CancellationToken ct = default)
    {
        var q = _db.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.EmissionPoint).ThenInclude(ep => ep!.Establishment)
            .Include(x => x.DefaultWarehouse)
            .Include(x => x.DefaultCustomer)
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

        if (activeFilter.HasValue)
            q = q.Where(x => x.IsActive == activeFilter.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Code, $"%{term}%") ||
                EF.Functions.ILike(x.Name, $"%{term}%") ||
                EF.Functions.ILike(x.Branch.Name, $"%{term}%"));
        }

        return await q.OrderBy(x => x.Branch.Name).ThenBy(x => x.Code).ToListAsync(ct);
    }

    public Task<bool> ExistsByCodeAsync(
        Guid tenantId, Guid branchId, string code, Guid? exceptId = null, CancellationToken ct = default)
    {
        var q = _db.CashRegisters
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.BranchId == branchId && x.Code == code);

        if (exceptId.HasValue)
            q = q.Where(x => x.Id != exceptId.Value);

        return q.AnyAsync(ct);
    }

    public Task AddAsync(CashRegister cashRegister, CancellationToken ct = default)
        => _db.CashRegisters.AddAsync(cashRegister, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
