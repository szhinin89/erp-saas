using ERP.Domain.Common;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Entities;

public sealed class CommunicationOutboxAttachment
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int FileNameMaxLen = 255;
    public const int ContentTypeMaxLen = 120;
    public const int FileStoragePathMaxLen = 1000;

    public Guid CompanyId { get; private set; }
    public Guid CommunicationOutboxId { get; private set; }
    public CommunicationAttachmentType AttachmentType { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string? FileStoragePath { get; private set; }
    public byte[]? BinaryContent { get; private set; }

    private CommunicationOutboxAttachment() { }

    internal static CommunicationOutboxAttachment Create(
        Guid tenantId,
        Guid companyId,
        Guid communicationOutboxId,
        CommunicationAttachmentType attachmentType,
        string fileName,
        string contentType,
        string? fileStoragePath,
        byte[]? binaryContent,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("El nombre del adjunto es obligatorio.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("El content type del adjunto es obligatorio.", nameof(contentType));

        if (string.IsNullOrWhiteSpace(fileStoragePath) && (binaryContent is null || binaryContent.Length == 0))
            throw new ArgumentException("El adjunto debe tener ruta de almacenamiento o contenido binario.", nameof(fileStoragePath));

        var attachment = new CommunicationOutboxAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CommunicationOutboxId = communicationOutboxId,
            AttachmentType = attachmentType,
            FileName = Trim(fileName, FileNameMaxLen, nameof(fileName)),
            ContentType = Trim(contentType, ContentTypeMaxLen, nameof(contentType)),
            FileStoragePath = NormalizeOptional(fileStoragePath, FileStoragePathMaxLen, nameof(fileStoragePath)),
            BinaryContent = binaryContent,
        };
        attachment.SetCreated(createdBy);
        return attachment;
    }

    private static string Trim(string value, int maxLength, string paramName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"El valor no puede superar {maxLength} caracteres.", paramName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? null : Trim(value, maxLength, paramName);
}
