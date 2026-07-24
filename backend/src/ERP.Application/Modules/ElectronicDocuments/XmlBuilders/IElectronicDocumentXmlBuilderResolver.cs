using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

/// <summary>
/// Localiza el <see cref="IElectronicDocumentXmlBuilder"/> registrado en DI para un
/// <see cref="ElectronicDocumentType"/>. Mismo patrón que
/// <c>IElectronicDocumentDataProviderResolver</c> — incorporar un nuevo tipo de comprobante
/// solo requiere registrar un nuevo builder, nunca tocar esta clase.
/// </summary>
public interface IElectronicDocumentXmlBuilderResolver
{
    IElectronicDocumentXmlBuilder? Resolve(ElectronicDocumentType documentType);
}
