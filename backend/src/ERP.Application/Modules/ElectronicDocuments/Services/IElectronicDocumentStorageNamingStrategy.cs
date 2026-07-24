using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Genera únicamente el nombre/ruta lógica (relativa) que se le pasará a
/// <c>IFileStorage.SaveAsync</c> — nunca accede al sistema de archivos ni escribe nada.
/// Permite cambiar la convención de nombres sin tocar el resto del módulo: solo esta clase
/// se reemplaza.
/// </summary>
public interface IElectronicDocumentStorageNamingStrategy
{
    string BuildRelativePath(
        Guid tenantId, ElectronicDocumentType documentType, Guid electronicDocumentId, ElectronicDocumentXmlVariant variant);
}
