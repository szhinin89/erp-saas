using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

public sealed class ElectronicDocumentDataProviderResolver : IElectronicDocumentDataProviderResolver
{
    private readonly IReadOnlyDictionary<ElectronicDocumentType, IElectronicDocumentDataProvider> _providers;

    public ElectronicDocumentDataProviderResolver(IEnumerable<IElectronicDocumentDataProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.DocumentType);
    }

    public IElectronicDocumentDataProvider? Resolve(ElectronicDocumentType documentType)
        => _providers.TryGetValue(documentType, out var provider) ? provider : null;
}
