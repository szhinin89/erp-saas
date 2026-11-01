using ERP.Application.Modules.ElectronicInvoicing.Enums;

namespace ERP.Application.Modules.ElectronicInvoicing.DTOs;

/// <summary>
/// Salud técnica de los servicios SRI que usa la empresa actual, en su ambiente configurado.
/// Sin datos sensibles: ni host, ni claves de acceso, ni documentos, ni mensajes del SRI.
/// </summary>
public sealed record SriServiceHealthDto(
    string? Environment,
    string? EnvironmentName,
    SriServiceStatusDto Reception,
    SriServiceStatusDto Authorization
);

/// <summary><see cref="LastObservedAtUtc"/> es el instante de la observación más reciente usada como evidencia.</summary>
public sealed record SriServiceStatusDto(
    SriServiceHealthState State,
    SriServiceHealthReason Reason,
    SriHealthEvidence Evidence,
    DateTime? LastObservedAtUtc
);
