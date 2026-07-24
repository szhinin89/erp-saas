using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Localiza el <see cref="IElectronicDocumentDataProvider"/> registrado en DI para un
/// <see cref="ElectronicDocumentType"/>. Incorporar un nuevo tipo de comprobante solo
/// requiere registrar un nuevo proveedor en DependencyInjection — nunca tocar esta clase
/// ni el resto del núcleo de ElectronicDocuments.
/// </summary>
public interface IElectronicDocumentDataProviderResolver
{
    /// <summary>Devuelve el proveedor del tipo, o <c>null</c> si ningún módulo lo ha registrado todavía.</summary>
    IElectronicDocumentDataProvider? Resolve(ElectronicDocumentType documentType);
}
