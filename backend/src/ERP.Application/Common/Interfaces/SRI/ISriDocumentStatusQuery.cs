using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>
/// Resultado tipado de <see cref="ISriDocumentStatusQuery.QueryAsync"/>. <see cref="FiscalStatus"/> solo
/// es distinto de <see cref="SriFiscalStatus.Unknown"/> cuando <see cref="Outcome"/> es
/// <see cref="SriStatusQueryOutcome.Success"/>. Los literales crudos del SRI se conservan como evidencia.
/// </summary>
public sealed record SriDocumentStatusResult
{
    public required SriStatusQueryOutcome Outcome { get; init; }
    public SriFiscalStatus FiscalStatus { get; init; } = SriFiscalStatus.Unknown;

    /// <summary>Literal <c>estadoAutorizacion</c> tal como lo envió el SRI (p.ej. "PENDIENTE DE ANULAR").</summary>
    public string? RawAuthorizationStatus { get; init; }

    /// <summary>Literal <c>estadoConsulta</c> (solo en respuestas de error, p.ej. "RECHAZADA").</summary>
    public string? RawQueryStatus { get; init; }

    public string? AccessKey { get; init; }
    public string? DocumentType { get; init; }
    public string? IssuerRuc { get; init; }

    /// <summary><c>fechaAutorizacion</c> del comprobante convertida a UTC (no es la fecha de anulación).</summary>
    public DateTime? AuthorizationDateUtc { get; init; }

    public IReadOnlyList<SriMessage> Messages { get; init; } = Array.Empty<SriMessage>();

    /// <summary>Descripción técnica del fallo cuando <see cref="Outcome"/> no es Success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Cuerpo SOAP crudo de la respuesta (evidencia técnica); null si no hubo respuesta.</summary>
    public string? RawResponse { get; init; }

    public bool IsSuccess => Outcome == SriStatusQueryOutcome.Success;
}

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01B — consulta del estado fiscal OFICIAL de un comprobante en el WS
/// ConsultaComprobante del SRI (<c>consultarEstadoAutorizacionComprobante(claveAcceso)</c>, Ficha
/// Técnica Comprobantes Electrónicos Esquema Offline v2.34 §8). SSOT tipado del estado fiscal
/// (AUTORIZADO / NO AUTORIZADO / PENDIENTE DE ANULAR / ANULADO). Solo CONSULTA: el SRI no ofrece un WS
/// para SOLICITAR la anulación. Implementado sobre el mismo <c>SriSoapClient</c> que recepción y
/// autorización — nunca un segundo cliente SOAP. Nunca lanza por fallos de transporte.
/// </summary>
public interface ISriDocumentStatusQuery
{
    /// <param name="accessKey">Clave de acceso de 49 dígitos.</param>
    /// <param name="wsdlUrl"><c>SriSettings.WsdlUrl</c> de la empresa (RecepcionComprobantesOffline); el endpoint de consulta se deriva de él (mismo ambiente).</param>
    /// <param name="ct"></param>
    Task<SriDocumentStatusResult> QueryAsync(
        string accessKey,
        string wsdlUrl,
        CancellationToken ct = default
    );
}
