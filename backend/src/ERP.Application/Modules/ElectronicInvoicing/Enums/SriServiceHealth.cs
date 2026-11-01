namespace ERP.Application.Modules.ElectronicInvoicing.Enums;

/// <summary>Estado técnico observado de un servicio SRI. Nunca expresa una decisión fiscal.</summary>
public enum SriServiceHealthState
{
    Unverified = 0,
    Available = 1,
    Slow = 2,
    Intermittent = 3,
    Unavailable = 4,
}

/// <summary>Tipo de evidencia que sustenta el estado: llamada SOAP real, ping al WSDL o ninguna.</summary>
public enum SriHealthEvidence
{
    None = 0,
    Call = 1,
    Probe = 2,
}

/// <summary>Motivo del estado — el frontend lo traduce a un texto comprensible (registro de presentación).</summary>
public enum SriServiceHealthReason
{
    NotConfigured = 0,
    NoRecentEvidence = 1,
    RecentResponses = 2,
    RespondedWithoutDecision = 3,
    SlowResponse = 4,
    RecentTransportFailure = 5,
    RecentInvalidResponse = 6,
    RecentUndeterminedFailure = 7,
    RecoveredAfterFailures = 8,
    ConsecutiveTransportFailures = 9,
    ProbeReachable = 10,
    ProbeUnreachable = 11,
    ProbeReachableAfterFailures = 12,
}
