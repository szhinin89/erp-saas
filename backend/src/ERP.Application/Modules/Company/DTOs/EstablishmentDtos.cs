namespace ERP.Application.Modules.Company.DTOs;

public record EstablishmentDto(
    Guid    Id,
    Guid    BranchId,
    Guid    CompanyId,
    string  Code,
    string  Name,
    string  Address,
    string? Phone,
    bool    IsMain,
    bool    IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt);
