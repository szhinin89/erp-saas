namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>Servicio SOAP del SRI observado por el monitor de salud.</summary>
public enum SriServiceKind
{
    Reception = 1,
    Authorization = 2,
}

/// <summary>
/// Clasificación técnica de UNA llamada al SRI. Nunca es una decisión fiscal: un DEVUELTA o un
/// NO AUTORIZADO son <see cref="Responded"/> — el servicio funcionó, el comprobante no.
/// </summary>
public enum SriObservationOutcome
{
    /// <summary>Respuesta SOAP válida (RECIBIDA/DEVUELTA/AUTORIZADO/NO AUTORIZADO/consulta interpretable).</summary>
    Responded = 1,

    /// <summary>
    /// Polling de autorización agotado: todas las respuestas fueron SOAP válidas pero sin decisión
    /// fiscal (PENDIENTE/EN PROCESO o vacías). El servicio respondió — no demuestra lentitud ni caída.
    /// </summary>
    RespondedWithoutDecision = 2,

    /// <summary>SOAP Fault real o cuerpo no interpretable: el servicio contestó, pero de forma anómala.</summary>
    InvalidResponse = 3,

    /// <summary>Sin respuesta: red, timeout HTTP o HTTP no-2xx sin SOAP Fault (ya tras los reintentos del cliente).</summary>
    TransportFailure = 4,

    /// <summary>Fallo que el contrato no permite clasificar (p. ej. consulta de decisiones no exitosa).</summary>
    Undetermined = 5,

    /// <summary>Ping al WSDL respondió (evidencia débil: solo alcance del endpoint).</summary>
    ProbeReachable = 6,

    /// <summary>Ping al WSDL no respondió — por sí solo nunca diagnostica una caída.</summary>
    ProbeUnreachable = 7,
}

/// <summary>
/// Registro pasivo de evidencia de comunicación con el SRI. Lo alimentan únicamente los
/// decoradores de los clientes SOAP existentes — nunca origina una llamada. No recibe claves de
/// acceso, XML, empresa ni mensajes del SRI: solo URL configurada (para derivar el host), servicio,
/// clasificación y duración. Nunca lanza: un fallo del registro jamás altera la llamada observada.
/// </summary>
public interface ISriServiceHealthRecorder
{
    /// <param name="wsdlUrl"><c>SriSettings.WsdlUrl</c> usada en la llamada; si no es una URL absoluta la observación se descarta.</param>
    /// <param name="service">Servicio SRI que atendió (o debía atender) la llamada.</param>
    /// <param name="outcome">Clasificación técnica de la llamada.</param>
    /// <param name="elapsed">Duración de una llamada individual; <c>null</c> cuando la duración incluye esperas deliberadas (polling) y no mide al servicio.</param>
    void Record(
        string? wsdlUrl,
        SriServiceKind service,
        SriObservationOutcome outcome,
        TimeSpan? elapsed
    );
}
