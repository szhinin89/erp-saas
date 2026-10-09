using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IBusinessPartnerImportLookup"/>
public sealed class BusinessPartnerImportLookup : IBusinessPartnerImportLookup
{
    private readonly ErpDbContext _db;

    public BusinessPartnerImportLookup(ErpDbContext db) => _db = db;

    public async Task<BusinessPartnerImportMatch?> FindByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        RoleType role,
        CancellationToken ct
    )
    {
        if (role is not (RoleType.Customer or RoleType.Supplier))
            throw new ArgumentOutOfRangeException(nameof(role), role, "Solo Cliente o Proveedor.");

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
            return new BusinessPartnerImportMatch(bp.Id, bp.IsActive, bp.Number, bp.LegalName,
                false, false, null, IsAmbiguous: true);

        var hasActiveRole = await _db
            .BusinessPartnerRoles.AsNoTracking()
            .AnyAsync(r => r.BusinessPartnerId == bp.Id && r.RoleType == role && r.IsActive, ct);
        var settings = role == RoleType.Customer
            ? await _db.CompanyBpSalesSettings.AsNoTracking()
                .Where(s => s.BusinessPartnerId == bp.Id)
                .Select(s => new { s.PaymentTermId })
                .FirstOrDefaultAsync(ct)
            : await _db.CompanyBpPurchaseSettings.AsNoTracking()
                .Where(s => s.BusinessPartnerId == bp.Id)
                .Select(s => new { s.PaymentTermId })
                .FirstOrDefaultAsync(ct);

        return new BusinessPartnerImportMatch(
            bp.Id,
            bp.IsActive,
            bp.Number,
            bp.LegalName,
            hasActiveRole,
            settings is not null,
            settings?.PaymentTermId
        );
    }
}
