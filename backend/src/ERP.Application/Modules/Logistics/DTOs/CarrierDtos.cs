namespace ERP.Application.Modules.Logistics.DTOs;

public record CarrierDto(
    Guid    Id,
    string  IdentificationType,
    string  IdentificationNumber,
    string  LegalName,
    string  LicensePlate,
    string? Phone,
    string? Email,
    bool    IsActive);
