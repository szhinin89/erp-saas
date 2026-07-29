using ERP.Domain.Common;
using ERP.Domain.Modules.Media.Enums;

namespace ERP.Domain.Modules.Media.Entities;

/// <summary>
/// Archivo multimedia (imagen, documento, etc.) asociado a una entidad propietaria
/// (<see cref="OwnerType"/> + <see cref="OwnerId"/>) dentro de una empresa.
/// El almacenamiento físico es opaco: <see cref="StoragePath"/> solo tiene sentido
/// para el servicio de almacenamiento de archivos (IFileStorage).
/// </summary>
public sealed class MediaFile : MasterEntity, ICompanyOperationalEntity
{
    public const int MaxFileNameLength = 260;
    public const int MaxContentTypeLength = 100;
    public const int MaxStorageProviderLength = 30;
    public const int MaxStoragePathLength = 500;
    public const int MaxPublicUrlLength = 500;
    public const int MaxRoleLength = 40;
    public const int MaxChecksumLength = 64;
    public const int MaxAltTextLength = 200;

    public Guid CompanyId { get; private set; }

    public string FileName { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }

    public string StorageProvider { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public string? PublicUrl { get; private set; }

    public MediaType MediaType { get; private set; }
    public MediaVisibility Visibility { get; private set; }

    public MediaOwnerType OwnerType { get; private set; }
    public Guid? OwnerId { get; private set; }
    public string? Role { get; private set; }

    public int DisplayOrder { get; private set; }
    public bool IsPrimary { get; private set; }

    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string? Checksum { get; private set; }
    public string? AltText { get; private set; }

    private MediaFile() { }

    public static MediaFile Create(
        Guid tenantId,
        Guid companyId,
        string fileName,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storageProvider,
        string storagePath,
        MediaType mediaType,
        MediaVisibility visibility,
        MediaOwnerType ownerType,
        Guid? ownerId,
        string? role,
        bool isPrimary,
        Guid createdBy,
        int? width = null,
        int? height = null,
        string? checksum = null,
        string? altText = null,
        int displayOrder = 0
    )
    {
        var media = new MediaFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            FileName = fileName.Trim(),
            OriginalFileName = originalFileName.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            StorageProvider = storageProvider.Trim(),
            StoragePath = storagePath.Trim(),
            MediaType = mediaType,
            Visibility = visibility,
            OwnerType = ownerType,
            OwnerId = ownerId,
            Role = role?.Trim() is { Length: > 0 } r ? r : null,
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder,
            Width = width,
            Height = height,
            Checksum = checksum?.Trim() is { Length: > 0 } c ? c : null,
            AltText = altText?.Trim() is { Length: > 0 } a ? a : null,
        };
        media.SetCreated(createdBy);
        return media;
    }
}
