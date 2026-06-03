using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.DTOs;

public sealed record BpLocationDto(
    Guid         Id,
    Guid         BusinessPartnerId,
    Guid         CompanyId,
    string       Name,
    LocationType Type,
    string       TypeLabel,
    string       Address,
    string?      ProvinceCode,
    string?      CantonCode,
    string?      ParishCode,
    string?      Phone,
    string?      Email,
    bool         IsPrimary,
    bool         IsActive,
    DateTime     CreatedAt)
{
    public static BpLocationDto From(BusinessPartnerLocation l) => new(
        l.Id, l.BusinessPartnerId, l.CompanyId,
        l.Name, l.Type, l.Type.ToString(),
        l.Address, l.ProvinceCode, l.CantonCode, l.ParishCode,
        l.Phone, l.Email, l.IsPrimary, l.IsActive, l.CreatedAt);
}

public sealed record BpContactDto(
    Guid        Id,
    Guid        BusinessPartnerId,
    Guid        CompanyId,
    Guid?       LocationId,
    string      FirstName,
    string?     LastName,
    string      FullName,
    string?     Position,
    ContactRole Role,
    string      RoleLabel,
    string?     Phone,
    string?     Mobile,
    string?     Email,
    string?     Notes,
    bool        IsPrimary,
    bool        IsActive,
    DateTime    CreatedAt)
{
    public static BpContactDto From(BusinessPartnerContact c) => new(
        c.Id, c.BusinessPartnerId, c.CompanyId, c.LocationId,
        c.FirstName, c.LastName, c.FullName,
        c.Position, c.Role, c.Role.ToString(),
        c.Phone, c.Mobile, c.Email, c.Notes,
        c.IsPrimary, c.IsActive, c.CreatedAt);
}
