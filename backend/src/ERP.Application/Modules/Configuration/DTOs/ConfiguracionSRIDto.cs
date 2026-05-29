namespace ERP.Application.Configuration.DTOs;

public record SriConfigurationDto(
    Guid   CompanyId,
    string CertificateP12Path,
    int    Environment,
    int    EmissionType,
    string SriAuthorizationUrl);
