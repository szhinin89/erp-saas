using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.SchemaValidation;

/// <summary>
/// Localiza el <see cref="IElectronicDocumentSchemaValidator"/> registrado en DI para un
/// <see cref="ElectronicDocumentType"/>. Mismo patrón que
/// <c>IElectronicDocumentDataProviderResolver</c> e <c>IElectronicDocumentXmlBuilderResolver</c>
/// — incorporar un nuevo tipo de comprobante solo requiere registrar un nuevo validador,
/// nunca tocar esta clase.
/// </summary>
public interface IElectronicDocumentSchemaValidatorResolver
{
    IElectronicDocumentSchemaValidator? Resolve(ElectronicDocumentType documentType);
}
