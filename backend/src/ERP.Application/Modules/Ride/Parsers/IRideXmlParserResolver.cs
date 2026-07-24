using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Application.Modules.Ride.Parsers;

/// <summary>
/// Localiza el <see cref="IRideXmlParser"/> registrado en DI para un <see cref="RideDocumentType"/>.
/// Mismo patrón que <c>IElectronicDocumentXmlBuilderResolver</c> (ElectronicDocuments, FROZEN) —
/// incorporar un nuevo tipo de comprobante solo requiere registrar un nuevo parser, nunca tocar
/// esta clase (ADR-025 §9/§10).
/// </summary>
public interface IRideXmlParserResolver
{
    IRideXmlParser? Resolve(RideDocumentType documentType);
}
