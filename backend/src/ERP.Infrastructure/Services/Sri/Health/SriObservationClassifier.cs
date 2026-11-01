using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri.Health;

/// <summary>
/// Traduce los estados literales que ya devuelven los adaptadores SOAP (ver
/// <see cref="SriSoapClient"/>) a evidencia técnica. Una decisión fiscal negativa (DEVUELTA,
/// NO AUTORIZADO) es una respuesta válida del servicio; un estado no reconocido nunca se asume como
/// éxito ni como caída.
/// </summary>
internal static class SriObservationClassifier
{
    public static SriObservationOutcome FromReceptionStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "RECIBIDA" or "DEVUELTA" => SriObservationOutcome.Responded,
            _ => FromTransportStatus(status),
        };

    public static SriObservationOutcome FromAuthorizationStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "AUTORIZADO" or "NO AUTORIZADO" => SriObservationOutcome.Responded,
            // Polling agotado con respuestas SOAP válidas sin decisión (el cliente devuelve
            // ERROR_CONEXION de inmediato si alguna consulta no tuvo respuesta).
            "TIMEOUT" => SriObservationOutcome.RespondedWithoutDecision,
            _ => FromTransportStatus(status),
        };

    private static SriObservationOutcome FromTransportStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "ERROR_CONEXION" => SriObservationOutcome.TransportFailure,
            "ERROR_SOAP_FAULT" or "ERROR_RESPUESTA_INVALIDA" => SriObservationOutcome.InvalidResponse,
            _ => SriObservationOutcome.Undetermined,
        };
}
