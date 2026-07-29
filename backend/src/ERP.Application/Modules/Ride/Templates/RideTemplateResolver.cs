using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Diccionario en memoria por <see cref="RideDocumentType"/>, construido desde las plantillas
/// registradas en DI — sin ninguna plantilla concreta todavía (llega en la Fase 6 del plan de
/// implementación), <see cref="Resolve"/> siempre devuelve <see langword="null"/>, nunca lanza.
/// La resolución jerárquica completa de ADR-025 §11 (empresa/sucursal/punto de emisión) es una
/// implementación futura de esta misma clase, no un contrato distinto.
/// </summary>
public sealed class RideTemplateResolver : IRideTemplateResolver
{
    private readonly IReadOnlyDictionary<RideDocumentType, IRideTemplate> _templates;

    public RideTemplateResolver(IEnumerable<IRideTemplate> templates)
    {
        _templates = templates.ToDictionary(t => t.DocumentType);
    }

    public IRideTemplate? Resolve(RideTemplateSelector selector) =>
        _templates.TryGetValue(selector.DocumentType, out var template) ? template : null;
}
