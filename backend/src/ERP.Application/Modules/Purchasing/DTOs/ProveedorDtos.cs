namespace ERP.Application.Modules.Purchasing.DTOs;

public record SupplierDto(
    Guid    Id,
    string  PersonType,
    string  LegalName,
    string  Ruc,
    string? Email,
    string? Phone,
    string? Address,
    string  PaymentTerms,
    bool    IsActive);

public record SupplierDetailDto(
    Guid      Id,
    string    PersonType,
    string    LegalName,
    string    Ruc,
    string?   Email,
    string?   Phone,
    string?   Address,
    string    PaymentTerms,
    bool      IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt,
    Guid      CreatedBy,
    Guid?     UpdatedBy);

