namespace ERP.Application.Modules.Sales.DTOs;

public record CustomerDto(
    Guid Id,
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? AddressLine,
    string? Phone,
    string? Email,
    string? Notes,
    bool IsActive);

public record CustomerDetailDto(
    Guid Id,
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? AddressLine,
    string? Phone,
    string? Email,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy);
