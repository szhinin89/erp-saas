namespace ERP.Application.Modules.ElectronicInvoicing.DTOs;

public record SriConfigurationDto(
    Guid CompanyId,
    bool HasCertificate,
    string? CertFileName,
    long? CertSizeBytes,
    DateTime? CertUploadedAtUtc,
    int Environment,
    int EmissionType,
    string WsdlUrl
);
