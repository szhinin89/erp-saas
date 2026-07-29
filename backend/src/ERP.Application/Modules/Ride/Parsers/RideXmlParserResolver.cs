using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Application.Modules.Ride.Parsers;

/// <summary>
/// Diccionario en memoria construido desde los parsers registrados en DI — sin ningún parser
/// concreto todavía (llegan en la Fase 6 del plan de implementación), <see cref="Resolve"/>
/// siempre devuelve <see langword="null"/>, nunca lanza. Mismo patrón exacto que
/// <c>ElectronicDocumentXmlBuilderResolver</c> (ElectronicDocuments, FROZEN).
/// </summary>
public sealed class RideXmlParserResolver : IRideXmlParserResolver
{
    private readonly IReadOnlyDictionary<RideDocumentType, IRideXmlParser> _parsers;

    public RideXmlParserResolver(IEnumerable<IRideXmlParser> parsers)
    {
        _parsers = parsers.ToDictionary(p => p.DocumentType);
    }

    public IRideXmlParser? Resolve(RideDocumentType documentType) =>
        _parsers.TryGetValue(documentType, out var parser) ? parser : null;
}
