using ERP.Application.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Finance;

/// <summary>Implementación de <see cref="ICustomerAdvanceRepository"/> — ADR-052 §7.</summary>
public sealed class CustomerAdvanceRepository : ICustomerAdvanceRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public CustomerAdvanceRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<CustomerAdvance?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .CustomerAdvances.ForOperationalScope(tenantId, _company)
            .Include(x => x.Movements)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<CustomerAdvance?> GetByIdForUpdateAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    )
    {
        // Fail-closed: sin empresa operativa no se bloquea ni se devuelve nada.
        if (!_company.HasCompanyContext)
            return null;

        // Patrón oficial de saldos compartidos (backend.md): lock → lectura acotada → Reload.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM customer_advances WHERE tenant_id = {tenantId} AND company_id = {_company.CompanyId} AND id = {id} FOR UPDATE",
            ct
        );
        var advance = await GetByIdAsync(tenantId, id, ct);
        if (advance is null)
            return null;
        await _db.Entry(advance).ReloadAsync(ct);
        return advance;
    }

    public async Task<CustomerAdvance?> GetByClientRequestIdAsync(
        Guid tenantId,
        Guid clientRequestId,
        CancellationToken ct = default
    )
    {
        var advanceId = await _db
            .CustomerAdvanceMovements.ForOperationalScope(tenantId, _company)
            .Where(x => x.ClientRequestId == clientRequestId)
            .Select(x => (Guid?)x.CustomerAdvanceId)
            .FirstOrDefaultAsync(ct);
        return advanceId is { } id ? await GetByIdAsync(tenantId, id, ct) : null;
    }

    public async Task<(IReadOnlyList<CustomerAdvance> Items, int Total)> SearchAsync(
        Guid tenantId,
        CustomerAdvanceSearchCriteria criteria,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = _db.CustomerAdvances.ForOperationalScope(tenantId, _company).AsNoTracking();
        if (criteria.CustomerId is { } customerId)
            query = query.Where(x => x.CustomerId == customerId);
        if (criteria.Status is { } status)
            query = query.Where(x => x.Status == status);
        if (criteria.BranchId is { } branchId)
            query = query.Where(x => x.BranchId == branchId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.AdvanceDate)
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<decimal> GetAvailableBalanceAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default
    ) =>
        _db
            .CustomerAdvances.ForOperationalScope(tenantId, _company)
            .Where(x => x.CustomerId == customerId && x.Status == CustomerAdvanceStatus.Confirmed)
            .SumAsync(x => x.AvailableAmount, ct);

    public Task AddAsync(CustomerAdvance advance, CancellationToken ct = default) =>
        _db.CustomerAdvances.AddAsync(advance, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
