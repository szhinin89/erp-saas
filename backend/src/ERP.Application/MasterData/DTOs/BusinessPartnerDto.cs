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
    Guid?   CustomerProfileId = null,
    Guid?   SupplierProfileId = null,
    Guid?   LegacyCustomerId  = null,
    Guid?   LegacySupplierId  = null)
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

    public bool HasOperationalCustomerLink => LegacyCustomerId.HasValue && LegacyCustomerId != Guid.Empty;
    public bool HasOperationalSupplierLink => LegacySupplierId.HasValue && LegacySupplierId != Guid.Empty;
}
