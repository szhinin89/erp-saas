using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.CompanyConfig;

/// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/Subfase 5B).</summary>
public sealed class CompanySpecialTaxResponsibilityRepository
    : ICompanySpecialTaxResponsibilityRepository
{
    private readonly ErpDbContext _context;

    public CompanySpecialTaxResponsibilityRepository(ErpDbContext context) => _context = context;

    public async Task<IReadOnlyCollection<string>> GetResponsibleSriTaxCategoryCodesAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) =>
        await _context
            .CompanySpecialTaxResponsibilities.Where(x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.IsActive
                && x.IsResponsibleOnSales
            )
            .Select(x => x.SriTaxCategoryCode)
            .ToListAsync(cancellationToken);
}
