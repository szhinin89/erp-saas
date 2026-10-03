using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;

namespace ERP.Application.Modules.ElectronicDocuments.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — ElectronicDocuments entrega SU XML autorizado al enviar el correo: la
/// referencia es el <c>ElectronicDocument.Id</c>, el contenido se lee del almacenamiento oficial
/// (<see cref="IFileStorage"/>) byte a byte, sin copiarlo al outbox ni asumir el filesystem del nodo.
/// Adjunto obligatorio: si no puede obtenerse, la entrega falla como reintentable (nunca se envía sin
/// el XML que se prometió al encolar). Solo lectura: no toca estados, firma ni SRI.
/// </summary>
public sealed class ElectronicDocumentAuthorizedXmlAttachmentProvider : ICommunicationAttachmentContentProvider
{
    private readonly IElectronicDocumentRepository _documents;
    private readonly IFileStorage _fileStorage;

    public ElectronicDocumentAuthorizedXmlAttachmentProvider(IElectronicDocumentRepository documents, IFileStorage fileStorage)
    {
        _documents = documents;
        _fileStorage = fileStorage;
    }

    public CommunicationAttachmentType AttachmentType => CommunicationAttachmentType.AuthorizedXml;

    public async Task<CommunicationAttachmentResolution> ResolveAsync(
        CommunicationAttachmentReference reference,
        CancellationToken ct = default
    )
    {
        var document = await _documents.GetByIdAsync(reference.TenantId, reference.ReferenceId, ct);
        if (document is null || document.CompanyId != reference.CompanyId)
            throw new CommunicationAttachmentException("El documento electrónico del XML adjunto no existe en la empresa de la comunicación.");

        if (document.CurrentState != ElectronicDocumentState.Authorized || string.IsNullOrWhiteSpace(document.AuthorizedXmlPath))
            throw new CommunicationAttachmentException("El documento electrónico no tiene XML autorizado almacenado.");

        await using var stream = await _fileStorage.GetAsync(document.AuthorizedXmlPath, ct)
            ?? throw new CommunicationAttachmentException("El XML autorizado no está disponible en el almacenamiento.");
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return CommunicationAttachmentResolution.Resolved(buffer.ToArray());
    }
}
