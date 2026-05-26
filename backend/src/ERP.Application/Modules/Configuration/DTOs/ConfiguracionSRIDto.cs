namespace ERP.Application.Configuration.DTOs;

public record SriConfigurationDto(
    Guid    CompanyId,
    string  CompanyRuc,
    string  LegalName,
    string? TradeName,
    string  MainAddress,
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    string  CertificateP12Path,
    int     Environment,
    int     EmissionType,
    string  SriAuthorizationUrl);
