using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — implementación única de <see cref="IElectronicDocumentXmlSupplierResolver"/>.
/// Regla, en este orden (RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B, sección E):
/// <list type="number">
/// <item><description>Un <see cref="IElectronicDocumentXmlSupplier"/> registrado explícitamente
/// para ese <see cref="ElectronicDocumentType"/> (hoy: solo Retención) tiene prioridad.</description></item>
/// <item><description>Si no hay uno, se intenta el camino comercial: si tanto
/// <see cref="IElectronicDocumentDataProviderResolver"/> como <see cref="IElectronicDocumentXmlBuilderResolver"/>
/// tienen una implementación registrada para ese tipo (hoy: Factura, Nota de Crédito), se
/// devuelve un <see cref="CommercialElectronicDocumentXmlSupplier"/> instanciado al vuelo con
/// ambas.</description></item>
/// <item><description>Si ninguno de los dos caminos resuelve, se devuelve <see langword="null"/>
/// — nunca se lanza una excepción; el llamador (<c>ElectronicDocumentIssuer</c>) decide qué
/// significa "no hay generador de XML registrado para este tipo", igual que ya hace con los
/// otros resolutores de este módulo.</description></item>
/// </list>
///
/// Esta regla es general, no específica de Retención: un tipo documental futuro con su propia
/// forma de datos se resuelve exactamente igual, registrando su propio supplier explícito, sin
/// volver a tocar esta clase.
/// </summary>
public sealed class ElectronicDocumentXmlSupplierResolver : IElectronicDocumentXmlSupplierResolver
{
    private readonly IReadOnlyDictionary<
        ElectronicDocumentType,
        IElectronicDocumentXmlSupplier
    > _explicitSuppliers;
    private readonly IElectronicDocumentDataProviderResolver _dataProviderResolver;
    private readonly IElectronicDocumentXmlBuilderResolver _xmlBuilderResolver;

    public ElectronicDocumentXmlSupplierResolver(
        IEnumerable<IElectronicDocumentXmlSupplier> explicitSuppliers,
        IElectronicDocumentDataProviderResolver dataProviderResolver,
        IElectronicDocumentXmlBuilderResolver xmlBuilderResolver
    )
    {
        _explicitSuppliers = explicitSuppliers.ToDictionary(s => s.DocumentType);
        _dataProviderResolver = dataProviderResolver;
        _xmlBuilderResolver = xmlBuilderResolver;
    }

    public IElectronicDocumentXmlSupplier? Resolve(ElectronicDocumentType documentType)
    {
        if (_explicitSuppliers.TryGetValue(documentType, out var explicitSupplier))
            return explicitSupplier;

        var dataProvider = _dataProviderResolver.Resolve(documentType);
        var xmlBuilder = _xmlBuilderResolver.Resolve(documentType);
        return dataProvider is null || xmlBuilder is null
            ? null
            : new CommercialElectronicDocumentXmlSupplier(documentType, dataProvider, xmlBuilder);
    }
}
