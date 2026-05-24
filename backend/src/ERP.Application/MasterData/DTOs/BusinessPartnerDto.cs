using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.DTOs;

public sealed record BusinessPartnerDto(
    Guid    Id,
    string  IdentificationType,
    string  IdentificationNumber,
    string  SriIdCode,
    string  LegalName,
    string? TradeName,
    string? Email,
    string? Phone,
    string? CountryCode,
    bool    IsActive,
    bool    IsCustomer,
    bool    IsSupplier,
    DateTime CreatedAt,
    // Profile IDs
    Guid?   CustomerProfileId              = null,
    Guid?   SupplierProfileId              = null,
    // Customer profile data
    string? CustomerNotes                  = null,
    // Supplier profile SRI defaults
    string? DefaultTaxSupportCode          = null,
    string? DefaultRetentionVatCode        = null,
    string? DefaultRetentionIncomeCode     = null,
    string? SupplierPaymentTerms           = null)
{
    public static BusinessPartnerDto From(BusinessPartner bp) => new(
        bp.Id,
        bp.Identification.Type,
        bp.Identification.Number,
        bp.Identification.SriCode,
        bp.LegalName,
        bp.TradeName,
        bp.Email,
        bp.Phone,
        bp.CountryCode,
        bp.IsActive,
        bp.CustomerProfile is not null,
        bp.SupplierProfile is not null,
        bp.CreatedAt);
}
