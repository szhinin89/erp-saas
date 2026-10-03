namespace ERP.Domain.Modules.Communications.Enums;

/// <summary>
/// Clasificación de un intento de entrega fallido (ADR-039 D10). Decide si se reintenta:
/// Transient y Unknown reintentan hasta agotar <c>MaxRetries</c>; Permanent y Configuration son
/// terminales (Configuration requiere corregir la configuración y un requeue explícito).
/// </summary>
public enum CommunicationFailureCategory
{
    Transient = 1,
    Permanent = 2,
    Configuration = 3,
    Unknown = 4,
}
