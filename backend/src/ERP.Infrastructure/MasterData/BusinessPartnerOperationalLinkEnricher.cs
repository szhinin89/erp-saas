using ERP.Application.MasterData;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData;

public sealed class BusinessPartnerOperationalLinkEnricher : IBusinessPartnerOperationalLinkEnricher
{
    private readonly ErpDbContext _db;

    public BusinessPartnerOperationalLinkEnricher(ErpDbContext db) => _db = db;

    public async Task<BusinessPartnerDto> EnrichAsync(BusinessPartner partner, CancellationToken ct = default)
    {
        var list = await EnrichAsync(new[] { partner }, ct);
        return list[0];
    }

    public async Task<IReadOnlyList<BusinessPartnerDto>> EnrichAsync(
        IReadOnlyList<BusinessPartner> partners,
        CancellationToken ct = default)
    {
        if (partners.Count == 0)
            return Array.Empty<BusinessPartnerDto>();

        var bpIds = partners.Select(p => p.Id).Distinct().ToList();
        var subscriberId = partners[0].SubscriberId;

        var customerProfiles = await _db.CustomerProfiles.AsNoTracking()
            .Where(p => p.SubscriberId == subscriberId && bpIds.Contains(p.BusinessPartnerId))
            .ToListAsync(ct);

        var supplierProfiles = await _db.SupplierProfiles.AsNoTracking()
            .Where(p => p.SubscriberId == subscriberId && bpIds.Contains(p.BusinessPartnerId))
            .ToListAsync(ct);

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.SubscriberId == subscriberId && c.IsActive)
            .ToListAsync(ct);

        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(s => s.SubscriberId == subscriberId && s.IsActive)
            .ToListAsync(ct);

        var cpByBp = customerProfiles.ToDictionary(p => p.BusinessPartnerId);
        var spByBp = supplierProfiles.ToDictionary(p => p.BusinessPartnerId);

        return partners.Select(bp =>
        {
            cpByBp.TryGetValue(bp.Id, out var cp);
            spByBp.TryGetValue(bp.Id, out var sp);

            var legacyCustomer = customers.FirstOrDefault(c =>
                c.IdentificationType == bp.Identification.Type &&
                c.IdentificationNumber == bp.Identification.Number);

            var legacySupplier = suppliers.FirstOrDefault(s =>
                s.Ruc == bp.Identification.Number);

            return BusinessPartnerDto.From(bp) with
            {
                CustomerProfileId              = cp?.Id,
                SupplierProfileId              = sp?.Id,
                LegacyCustomerId               = legacyCustomer?.Id,
                LegacySupplierId               = legacySupplier?.Id,
                CustomerNotes                  = cp?.Notes,
                DefaultTaxSupportCode          = sp?.DefaultTaxSupportCode,
                DefaultRetentionVatCode        = sp?.DefaultRetentionVatCode,
                DefaultRetentionIncomeCode     = sp?.DefaultRetentionIncomeCode,
                SupplierPaymentTerms           = sp?.PaymentTerms,
            };
        }).ToList();
    }
}
