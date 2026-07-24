using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

public sealed class ElectronicDocumentXmlBuilderResolver : IElectronicDocumentXmlBuilderResolver
{
    private readonly IReadOnlyDictionary<ElectronicDocumentType, IElectronicDocumentXmlBuilder> _builders;

    public ElectronicDocumentXmlBuilderResolver(IEnumerable<IElectronicDocumentXmlBuilder> builders)
    {
        _builders = builders.ToDictionary(b => b.DocumentType);
    }

    public IElectronicDocumentXmlBuilder? Resolve(ElectronicDocumentType documentType)
        => _builders.TryGetValue(documentType, out var builder) ? builder : null;
}
