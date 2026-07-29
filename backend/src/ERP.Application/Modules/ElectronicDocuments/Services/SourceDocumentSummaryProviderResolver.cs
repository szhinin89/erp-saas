namespace ERP.Application.Modules.ElectronicDocuments.Services;

public sealed class SourceDocumentSummaryProviderResolver : ISourceDocumentSummaryProviderResolver
{
    private readonly IReadOnlyDictionary<string, ISourceDocumentSummaryProvider> _providers;

    public SourceDocumentSummaryProviderResolver(
        IEnumerable<ISourceDocumentSummaryProvider> providers
    )
    {
        _providers = providers.ToDictionary(p => p.SourceModule);
    }

    public ISourceDocumentSummaryProvider? Resolve(string sourceModule) =>
        _providers.TryGetValue(sourceModule, out var provider) ? provider : null;
}
