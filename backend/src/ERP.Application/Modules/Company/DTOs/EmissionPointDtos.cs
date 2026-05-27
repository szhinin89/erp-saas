namespace ERP.Application.Modules.Company.DTOs;

public record EmissionPointDto(
    Guid    Id,
    Guid    EstablishmentId,
    Guid    CompanyId,
    string  Code,
    string? Name,
    bool    IsDefault,
    bool    IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt);
