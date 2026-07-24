namespace ERP.Application.Modules.ElectronicInvoicing.DTOs;

/// <summary>
/// Resultado de inspeccionar el certificado con una contraseña (guardada o recién escrita) —
/// nunca incluye el archivo ni la contraseña, solo metadatos de diagnóstico.
/// </summary>
public sealed record SriCertificateInfoDto(
    bool      PasswordCorrect,
    bool      Loaded,
    DateTime? NotAfterUtc,
    int?      DaysRemaining,
    string?   Subject,
    string?   Issuer,
    string?   ErrorMessage);

public sealed record SriCertificateUploadResultDto(
    string    FileName,
    long      SizeBytes,
    DateTime  UploadedAtUtc,
    SriCertificateInfoDto? Inspection);
