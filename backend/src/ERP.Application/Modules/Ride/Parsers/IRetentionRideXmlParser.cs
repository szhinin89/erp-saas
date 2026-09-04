using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Parsers;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — parsea el XML autorizado de Comprobante de Retención a
/// <see cref="RetentionRideModel"/>.
///
/// Deliberadamente NO implementa <see cref="IRideXmlParser"/> (esa interfaz está fija a
/// <c>Result&lt;RideModel&gt;</c>, la forma comercial de Factura/Nota de Crédito — incompatible
/// con <c>RetentionRideModel</c>). Mismo criterio de fork ya adoptado en
/// RETENTIONS-SRI-XML-MAPPER-03B para <c>IRetentionXmlBuilder</c> frente a
/// <c>IElectronicDocumentXmlBuilder</c>: contrato propio, sin tocar <see cref="IRideXmlParserResolver"/>
/// (motor genérico de Ride) ni <c>RidePipeline</c> en esta fase — esa decisión de wiring final
/// queda pendiente y deliberadamente no se activa aquí.
/// </summary>
public interface IRetentionRideXmlParser
{
    RideDocumentType DocumentType { get; }

    Result<RetentionRideModel> Parse(string authorizedXml);
}
