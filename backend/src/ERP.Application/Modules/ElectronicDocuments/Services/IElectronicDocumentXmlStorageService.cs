using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Persiste el XML generado y el XML firmado de un documento electrónico usando exclusivamente
/// <c>IFileStorage</c> — nunca un segundo sistema de almacenamiento ni una ruta construida a mano.
/// </summary>
public interface IElectronicDocumentXmlStorageService
{
    Task<Result<ElectronicDocumentStoredXmlPaths>> StoreAsync(
        Guid tenantId,
        ElectronicDocumentType documentType,
        Guid electronicDocumentId,
        ElectronicDocumentXml draftXml,
        SignedElectronicDocumentXml signedXml,
        CancellationToken ct = default);

    /// <summary>
    /// Persiste el XML autorizado devuelto por el SRI (Fase 9) — independiente de
    /// <see cref="StoreAsync"/> porque llega en un momento distinto del ciclo de vida (tras la
    /// consulta de autorización, no junto con la firma).
    /// </summary>
    Task<Result<string>> StoreAuthorizedAsync(
        Guid tenantId,
        ElectronicDocumentType documentType,
        Guid electronicDocumentId,
        string authorizedXml,
        CancellationToken ct = default);
}
