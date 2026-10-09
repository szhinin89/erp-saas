using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="ICustomerImportLookup"/>
public sealed class CustomerImportLookup : ICustomerImportLookup
{
    private readonly ErpDbContext _db;

    public CustomerImportLookup(ErpDbContext db) => _db = db;

    public async Task<CustomerImportMatch?> FindByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken ct
    )
    {
        var number = identificationNumber.Trim().ToUpperInvariant();
        var matches = await _db
            .BusinessPartners.AsNoTracking()
            .Where(x =>
                x.Identification.Type == identificationType
                && x.Identification.Number.ToUpper() == number
            )
            .Select(x => new
            {
                x.Id,
                x.IsActive,
                x.Identification.Number,
                x.Name.LegalName,
            })
            .Take(2)
            .ToListAsync(ct);

        if (matches.Count == 0)
            return null;
        var bp = matches[0];
        if (matches.Count > 1)
            return new CustomerImportMatch(bp.Id, bp.IsActive, bp.Number, bp.LegalName,
                false, false, null, IsAmbiguous: true);

        var hasActiveCustomerRole = await _db
            .BusinessPartnerRoles.AsNoTracking()
            .AnyAsync(
                r => r.BusinessPartnerId == bp.Id && r.RoleType == RoleType.Customer && r.IsActive,
                ct
            );
        var settings = await _db
            .CompanyBpSalesSettings.AsNoTracking()
            .Where(s => s.BusinessPartnerId == bp.Id)
            .Select(s => new { s.PaymentTermId })
            .FirstOrDefaultAsync(ct);

        return new CustomerImportMatch(
            bp.Id,
            bp.IsActive,
            bp.Number,
            bp.LegalName,
            hasActiveCustomerRole,
            settings is not null,
            settings?.PaymentTermId
        );
    }
}
