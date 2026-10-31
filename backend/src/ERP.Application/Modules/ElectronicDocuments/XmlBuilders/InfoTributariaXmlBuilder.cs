using System.Xml.Linq;
using ERP.Application.Modules.ElectronicDocuments.DTOs;

namespace ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

/// <summary>
/// ADR-038 D8 — bloque <c>infoTributaria</c> común a Factura 1.1.0, Nota de crédito 1.1.0 y
/// Retención 1.0.0 (secuencia idéntica en los tres XSD oficiales). Extraído al agregar
/// <c>agenteRetencion</c> (R21); sin designación la salida es la misma que la anterior de cada
/// builder. Solo transcribe el modelo: nunca decide si una designación aplica.
/// </summary>
internal static class InfoTributariaXmlBuilder
{
    public static XElement Build(
        ElectronicDocumentEmissionContext emission,
        ElectronicDocumentIssuerData issuer,
        string accessKey
    ) =>
        new(
            "infoTributaria",
            new XElement("ambiente", emission.Environment),
            new XElement("tipoEmision", emission.EmissionType),
            new XElement("razonSocial", issuer.LegalName),
            issuer.TradeName is null ? null : new XElement("nombreComercial", issuer.TradeName),
            new XElement("ruc", issuer.TaxId),
            new XElement("claveAcceso", accessKey),
            new XElement("codDoc", emission.DocTypeCode),
            new XElement("estab", emission.Establishment),
            new XElement("ptoEmi", emission.EmissionPoint),
            new XElement("secuencial", emission.Sequential),
            new XElement("dirMatriz", issuer.MatrixAddress),
            // Ficha 2.34 Anexo 21: número de resolución sin ceros a la izquierda, después de
            // dirMatriz (el <regimenMicroempresas> que cita la ficha ya no existe en los XSD).
            string.IsNullOrWhiteSpace(issuer.RetentionAgentResolutionNumber)
                ? null
                : new XElement("agenteRetencion", issuer.RetentionAgentResolutionNumber),
            // Anexo 22: entre <agenteRetencion> y </infoTributaria>.
            issuer.TaxRegime is null ? null : new XElement("contribuyenteRimpe", issuer.TaxRegime)
        );
}
