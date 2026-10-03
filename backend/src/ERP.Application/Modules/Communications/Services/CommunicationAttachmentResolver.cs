using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Communications.Services;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — adjunto por referencia, tal como lo ve su módulo dueño al enviar:
/// tipo, recurso referenciado y origen de negocio de la comunicación (mismo par
/// <c>SourceModule</c>/<c>SourceId</c> que el documento electrónico). El alcance de la empresa ya lo
/// abrió el processor (JobExecutionContext de la fila).
/// </summary>
public sealed record CommunicationAttachmentReference(
    Guid TenantId,
    Guid CompanyId,
    CommunicationAttachmentType AttachmentType,
    Guid ReferenceId,
    string? SourceModule,
    Guid? SourceId
);

/// <summary>Contenido resuelto, u omisión explícita de un adjunto opcional (con motivo técnico, sin PII).</summary>
public sealed record CommunicationAttachmentResolution(byte[]? Content, string? SkippedReason)
{
    public static CommunicationAttachmentResolution Resolved(byte[] content) => new(content, null);

    public static CommunicationAttachmentResolution Skipped(string reason) => new(null, reason);
}

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — el módulo dueño de un recurso lo entrega al enviar (p. ej. el XML
/// autorizado lo resuelve ElectronicDocuments; el RIDE, Ride). Un proveedor por tipo de adjunto.
/// Contrato: <see cref="CommunicationAttachmentResolution.Skipped"/> solo para adjuntos opcionales;
/// un adjunto obligatorio que no puede obtenerse lanza <see cref="CommunicationAttachmentException"/>
/// (fallo reintentable de la entrega, nunca un correo con adjuntos inventados).
/// </summary>
public interface ICommunicationAttachmentContentProvider
{
    CommunicationAttachmentType AttachmentType { get; }

    Task<CommunicationAttachmentResolution> ResolveAsync(
        CommunicationAttachmentReference reference,
        CancellationToken ct = default
    );
}

/// <summary>Un adjunto no pudo resolverse al enviar (categoría Transient: se reintenta con backoff).</summary>
public sealed class CommunicationAttachmentException(string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string Code => ApiResponseCodes.Communications.AttachmentUnavailable;
}

/// <summary>Convierte los adjuntos persistidos de una comunicación en bytes listos para el transporte.</summary>
public interface ICommunicationAttachmentResolver
{
    Task<IReadOnlyList<EmailAttachment>> ResolveAsync(CommunicationOutbox communication, CancellationToken ct = default);
}

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — único punto que materializa adjuntos al enviar: bytes guardados tal cual,
/// rutas vía <see cref="IFileStorage"/> (almacenamiento oficial, nunca <c>File.Exists</c> del nodo) y
/// referencias vía el <see cref="ICommunicationAttachmentContentProvider"/> del tipo.
/// </summary>
public sealed partial class CommunicationAttachmentResolver : ICommunicationAttachmentResolver
{
    private readonly IFileStorage _fileStorage;
    private readonly IReadOnlyDictionary<CommunicationAttachmentType, ICommunicationAttachmentContentProvider> _providers;
    private readonly ILogger<CommunicationAttachmentResolver> _logger;

    public CommunicationAttachmentResolver(
        IFileStorage fileStorage,
        IEnumerable<ICommunicationAttachmentContentProvider> providers,
        ILogger<CommunicationAttachmentResolver> logger
    )
    {
        _fileStorage = fileStorage;
        // ToDictionary falla ante dos proveedores para el mismo tipo: un recurso tiene un solo dueño.
        _providers = providers.ToDictionary(p => p.AttachmentType);
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmailAttachment>> ResolveAsync(CommunicationOutbox communication, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(communication);
        var resolved = new List<EmailAttachment>(communication.Attachments.Count);

        foreach (var attachment in communication.Attachments)
        {
            var content = await ResolveContentAsync(communication, attachment, ct);
            if (content is not null)
                resolved.Add(new EmailAttachment(attachment.FileName, attachment.ContentType, content));
        }

        return resolved;
    }

    private async Task<byte[]?> ResolveContentAsync(
        CommunicationOutbox communication,
        CommunicationOutboxAttachment attachment,
        CancellationToken ct
    )
    {
        if (attachment.BinaryContent is { Length: > 0 } binary)
            return binary;

        if (attachment.ReferenceId is { } referenceId)
            return await ResolveReferenceAsync(communication, attachment, referenceId, ct);

        return await ReadStoredAsync(attachment.FileStoragePath!, ct);
    }

    private async Task<byte[]?> ResolveReferenceAsync(
        CommunicationOutbox communication,
        CommunicationOutboxAttachment attachment,
        Guid referenceId,
        CancellationToken ct
    )
    {
        if (communication.TenantId is not { } tenantId || communication.CompanyId is not { } companyId)
            throw new CommunicationAttachmentException("Un adjunto por referencia requiere una comunicación con alcance de empresa.");

        if (!_providers.TryGetValue(attachment.AttachmentType, out var provider))
            throw new CommunicationAttachmentException($"No hay proveedor para adjuntos {attachment.AttachmentType}.");

        var resolution = await provider.ResolveAsync(
            new CommunicationAttachmentReference(
                tenantId,
                companyId,
                attachment.AttachmentType,
                referenceId,
                communication.SourceModule,
                communication.SourceId
            ),
            ct
        );

        if (resolution.Content is { Length: > 0 } content)
            return content;

        LogAttachmentSkipped(communication.Id, attachment.AttachmentType, resolution.SkippedReason ?? "empty");
        return null;
    }

    private async Task<byte[]> ReadStoredAsync(string storedPath, CancellationToken ct)
    {
        await using var stream = await _fileStorage.GetAsync(storedPath, ct)
            ?? throw new CommunicationAttachmentException("El archivo adjunto no está disponible en el almacenamiento.");
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    // Sin nombres de archivo ni rutas: solo el tipo y el motivo técnico.
    [LoggerMessage(EventId = 4220, EventName = "CommunicationAttachmentSkipped", Level = LogLevel.Information,
        Message = "Communications: optional attachment {AttachmentType} omitted for {CommunicationId}: {Reason}")]
    private partial void LogAttachmentSkipped(Guid communicationId, CommunicationAttachmentType attachmentType, string reason);
}
