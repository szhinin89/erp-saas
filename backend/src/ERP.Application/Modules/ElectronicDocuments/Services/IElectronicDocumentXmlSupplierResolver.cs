using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — localiza el <see cref="IElectronicDocumentXmlSupplier"/>
/// que corresponde a un <see cref="ElectronicDocumentType"/>. Regla única (RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B,
/// sección E): un supplier explícitamente registrado tiene prioridad; si no hay uno, se cae al
/// camino comercial (<see cref="IElectronicDocumentDataProviderResolver"/> +
/// <see cref="XmlBuilders.IElectronicDocumentXmlBuilderResolver"/>) para ese tipo, si existe. Sin
/// esta regla genérica, incorporar Retención habría exigido un <c>if (documentType == Retention)</c>
/// dentro de <see cref="ElectronicDocumentIssuer"/> — esta interfaz existe precisamente para
/// evitarlo.
/// </summary>
public interface IElectronicDocumentXmlSupplierResolver
{
    IElectronicDocumentXmlSupplier? Resolve(ElectronicDocumentType documentType);
}
