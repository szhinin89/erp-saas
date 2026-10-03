using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.Communications.ElectronicDocuments;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — único enrutamiento (SourceModule, DocumentType) → contributor. Sin
/// switches por documento ni reflexión: cada contributor declara sus rutas y se registra en DI.
/// </summary>
public interface IElectronicDocumentCommunicationContributorResolver
{
    IElectronicDocumentCommunicationContributor? Resolve(string sourceModule, ElectronicDocumentType documentType);

    /// <summary>Todas las rutas soportadas (para la reconciliación).</summary>
    IReadOnlyList<ElectronicDocumentCommunicationRoute> Routes { get; }
}

public sealed record ElectronicDocumentCommunicationRoute(
    string SourceModule,
    ElectronicDocumentType DocumentType,
    string SourceType,
    string Purpose
);

public sealed class ElectronicDocumentCommunicationContributorResolver : IElectronicDocumentCommunicationContributorResolver
{
    private readonly IReadOnlyDictionary<(string, ElectronicDocumentType), IElectronicDocumentCommunicationContributor> _byRoute;

    public ElectronicDocumentCommunicationContributorResolver(IEnumerable<IElectronicDocumentCommunicationContributor> contributors)
    {
        var routes = new List<ElectronicDocumentCommunicationRoute>();
        var byRoute = new Dictionary<(string, ElectronicDocumentType), IElectronicDocumentCommunicationContributor>();
        foreach (var contributor in contributors)
        {
            foreach (var (documentType, target) in contributor.Targets)
            {
                // Una ruta = un dueño: dos contributors para el mismo par es un error de composición.
                if (!byRoute.TryAdd((contributor.SourceModule, documentType), contributor))
                    throw new InvalidOperationException(
                        $"Más de un contributor de comunicación para {contributor.SourceModule}/{documentType}."
                    );
                routes.Add(new ElectronicDocumentCommunicationRoute(contributor.SourceModule, documentType, target.SourceType, target.Purpose));
            }
        }

        _byRoute = byRoute;
        Routes = routes;
    }

    public IReadOnlyList<ElectronicDocumentCommunicationRoute> Routes { get; }

    public IElectronicDocumentCommunicationContributor? Resolve(string sourceModule, ElectronicDocumentType documentType) =>
        _byRoute.GetValueOrDefault((sourceModule, documentType));
}
