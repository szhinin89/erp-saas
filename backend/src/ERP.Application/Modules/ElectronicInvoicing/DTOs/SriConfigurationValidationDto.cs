namespace ERP.Application.Modules.ElectronicInvoicing.DTOs;

public sealed record SriConfigurationCheckDto(string Code, bool Passed, string Message);

public sealed record SriConfigurationValidationDto(
    bool IsValid,
    IReadOnlyList<SriConfigurationCheckDto> Checks,
    SriCertificateInfoDto? Certificate);
