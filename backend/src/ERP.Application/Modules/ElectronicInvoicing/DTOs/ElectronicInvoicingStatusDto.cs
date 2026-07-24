using ERP.Application.Modules.ElectronicInvoicing.Enums;

namespace ERP.Application.Modules.ElectronicInvoicing.DTOs;

/// <summary>
/// Contrato oficial y estable del estado operativo de facturación electrónica para UI
/// transversal. <see cref="Status"/> es el campo principal — calculado exclusivamente por
/// Application, nunca inferido en el frontend. El resto de los campos son detalle de
/// diagnóstico/informativo, preparados para futuras evoluciones sin romper el contrato.
/// Nunca incluye rutas del certificado, contraseñas ni datos sensibles.
/// </summary>
public sealed record ElectronicInvoicingStatusDto(
    ElectronicInvoicingStatus Status,
    bool Configured,
    string? Environment,
    string? EnvironmentName,
    string? EmissionType,
    bool CertificateInstalled,
    bool CertificateValid,
    DateTime? CertificateExpiresAt,
    int? CertificateDaysRemaining,
    SriAvailability SriAvailability,
    bool CanIssue);
