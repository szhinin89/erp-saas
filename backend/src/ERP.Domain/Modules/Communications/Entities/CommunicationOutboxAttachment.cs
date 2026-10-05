using ERP.Domain.Common;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Entities;

/// <summary>Adjunto de una comunicación; hereda el alcance (tenant/empresa o instancia) de su comunicación.</summary>
public sealed class CommunicationOutboxAttachment : SystemBaseEntity, IOptionalCompanyScopeEntity
{
    public const int FileNameMaxLen = 255;
    public const int ContentTypeMaxLen = 120;
    public const int FileStoragePathMaxLen = 1000;

    public Guid? TenantId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Guid CommunicationOutboxId { get; private set; }
    public CommunicationAttachmentType AttachmentType { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string? FileStoragePath { get; private set; }
    public byte[]? BinaryContent { get; private set; }

    /// <summary>
    /// ZH-EDOC-COMMUNICATIONS-01 — referencia lógica al recurso en su módulo dueño (p. ej. el
    /// ElectronicDocument para AuthorizedXml/RidePdf). El contenido lo resuelve ese módulo AL ENVIAR
    /// (ICommunicationAttachmentContentProvider), sobre su almacenamiento oficial: no se duplican bytes
    /// ni se asume el filesystem del nodo que encoló.
    /// </summary>
    public Guid? ReferenceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private CommunicationOutboxAttachment() { }

    internal static CommunicationOutboxAttachment Create(
        Guid? tenantId,
        Guid? companyId,
        Guid communicationOutboxId,
        CommunicationAttachmentType attachmentType,
        string fileName,
        string contentType,
        string? fileStoragePath,
        byte[]? binaryContent,
        Guid? referenceId,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("El nombre del adjunto es obligatorio.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException(
                "El content type del adjunto es obligatorio.",
                nameof(contentType)
            );

        if (
            string.IsNullOrWhiteSpace(fileStoragePath)
            && (binaryContent is null || binaryContent.Length == 0)
            && (referenceId is null || referenceId == Guid.Empty)
        )
            throw new ArgumentException(
                "El adjunto debe tener ruta de almacenamiento, contenido binario o referencia a su módulo dueño.",
                nameof(fileStoragePath)
            );

        return new CommunicationOutboxAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CommunicationOutboxId = communicationOutboxId,
            AttachmentType = attachmentType,
            FileName = Trim(fileName, FileNameMaxLen, nameof(fileName)),
            ContentType = Trim(contentType, ContentTypeMaxLen, nameof(contentType)),
            FileStoragePath = NormalizeOptional(
                fileStoragePath,
                FileStoragePathMaxLen,
                nameof(fileStoragePath)
            ),
            BinaryContent = binaryContent,
            ReferenceId = referenceId == Guid.Empty ? null : referenceId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    private static string Trim(string value, int maxLength, string paramName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException(
                $"El valor no puede superar {maxLength} caracteres.",
                paramName
            );
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? null : Trim(value, maxLength, paramName);
}
