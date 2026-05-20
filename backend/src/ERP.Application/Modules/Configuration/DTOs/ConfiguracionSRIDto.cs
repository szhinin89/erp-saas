namespace ERP.Application.Configuration.DTOs;

public record SriConfigurationDto(
    Guid    SubscriberId,
    string  CompanyRuc,
    string  LegalName,
    string? TradeName,
    string  MainAddress,
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    string    EstabCode,
    string    EmPointCode,
    int     CurrentSequential,
    string  CertificateP12Path,
    int     Environment,
    int     EmissionType,
    string  SriAuthorizationUrl);
