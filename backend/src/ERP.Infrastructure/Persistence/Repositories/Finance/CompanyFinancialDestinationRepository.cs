using ERP.Application.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Finance;

/// <summary>
/// Implementación de <see cref="ICompanyFinancialDestinationRepository"/> — diseño P0-02 §6.4,
/// Fase 2. El bloqueo <c>FOR SHARE</c> real (§6.4quater) se agrega en la Fase 8, junto con
/// <c>RegisterSupplierCreditRefundUseCases</c> — fuera del alcance de persistencia base.
/// </summary>
public sealed class CompanyFinancialDestinationRepository : ICompanyFinancialDestinationRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public CompanyFinancialDestinationRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<CompanyFinancialDestination?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .CompanyFinancialDestinations.ForOperationalScope(tenantId, _company)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<CompanyFinancialDestination?> GetByIdForShareAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    )
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM company_financial_destinations WHERE id = {id} AND tenant_id = {tenantId} FOR SHARE",
            ct
        );
        return await GetByIdAsync(tenantId, id, ct);
    }

    public Task AddAsync(
        CompanyFinancialDestination destination,
        CancellationToken ct = default
    ) => _db.CompanyFinancialDestinations.AddAsync(destination, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<CompanyFinancialDestination>> GetListAsync(
        Guid tenantId,
        bool? isActive,
        CancellationToken ct = default
    )
    {
        var query = _db.CompanyFinancialDestinations.ForOperationalScope(tenantId, _company);
        if (isActive is not null)
            query = query.Where(x => x.IsActive == isActive.Value);
        return await query.OrderBy(x => x.Code).ToListAsync(ct);
    }
}
