namespace ERP.Domain.Modules.ElectronicDocuments.Enums;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01B — resultado TÉCNICO de una consulta a ConsultaComprobante. Distinto
/// del estado fiscal (<see cref="SriFiscalStatus"/>): solo <see cref="Success"/> trae un estado fiscal
/// utilizable. Un rechazo de la consulta (<c>estadoConsulta=RECHAZADA</c>, código 99), un timeout o un
/// fallo de red NUNCA son un estado fiscal.
/// </summary>
public enum SriStatusQueryOutcome
{
    /// <summary>El SRI respondió con un <c>estadoAutorizacion</c> para la clave consultada.</summary>
    Success = 1,

    /// <summary><c>estadoConsulta=RECHAZADA</c> (p.ej. código 99: clave fuera de rango o sin datos).</summary>
    Rejected = 2,

    /// <summary>El servicio no respondió dentro del tiempo (tras los reintentos del cliente).</summary>
    Timeout = 3,

    /// <summary>Fallo de red, HTTP no-2xx, SOAP Fault o configuración SRI ausente.</summary>
    Unavailable = 4,

    /// <summary>Respuesta no interpretable o con un estado no reconocido.</summary>
    Unknown = 5,
}
