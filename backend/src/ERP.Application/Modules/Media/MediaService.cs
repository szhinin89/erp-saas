using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Media.Entities;
using ERP.Domain.Modules.Media.Enums;
using ERP.Domain.Modules.Media.Interfaces;

namespace ERP.Application.Modules.Media;

public sealed class MediaService : IMediaService
{
    private readonly IMediaFileRepository _mediaFiles;
    private readonly IFileStorage _fileStorage;
    private readonly IImageValidationService _imageValidation;

    public MediaService(
        IMediaFileRepository mediaFiles,
        IFileStorage fileStorage,
        IImageValidationService imageValidation
    )
    {
        _mediaFiles = mediaFiles;
        _fileStorage = fileStorage;
        _imageValidation = imageValidation;
    }

    public async Task<Result<MediaFile>> ReplacePrimaryImageAsync(
        ReplacePrimaryMediaRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var validation = _imageValidation.Validate(request.Content);
        if (!validation.IsSuccess)
            return Result<MediaFile>.ValidationFailure(validation.Error!);

        var metadata = validation.Value!;

        var existing = await _mediaFiles.GetActivePrimaryAsync(
            request.TenantId,
            request.CompanyId,
            request.OwnerType,
            request.OwnerId,
            request.Role,
            cancellationToken
        );

        var storedFileName = $"{Guid.NewGuid():N}.{metadata.Extension}";
        var relativePath = string.Join(
            '/',
            "media",
            request.TenantId.ToString("N"),
            request.OwnerType.ToString().ToLowerInvariant(),
            request.OwnerId.ToString("N"),
            request.Role,
            storedFileName
        );

        request.Content.Content.Position = 0;
        var storedPath = await _fileStorage.SaveAsync(
            relativePath,
            request.Content.Content,
            cancellationToken
        );

        var mediaFile = MediaFile.Create(
            tenantId: request.TenantId,
            companyId: request.CompanyId,
            fileName: storedFileName,
            originalFileName: request.Content.FileName,
            contentType: metadata.ContentType,
            sizeBytes: request.Content.SizeBytes,
            storageProvider: "local",
            storagePath: storedPath,
            mediaType: request.MediaType,
            visibility: request.Visibility,
            ownerType: request.OwnerType,
            ownerId: request.OwnerId,
            role: request.Role,
            isPrimary: true,
            createdBy: request.UserId,
            width: metadata.Width,
            height: metadata.Height
        );

        if (existing is not null)
            existing.Disable(request.UserId);

        await _mediaFiles.AddAsync(mediaFile, cancellationToken);
        await _mediaFiles.SaveChangesAsync(cancellationToken);

        return Result<MediaFile>.Success(mediaFile);
    }

    public Task<MediaFile?> GetActivePrimaryAsync(
        Guid tenantId,
        Guid companyId,
        MediaOwnerType ownerType,
        Guid ownerId,
        string role,
        CancellationToken cancellationToken = default
    ) =>
        _mediaFiles.GetActivePrimaryAsync(
            tenantId,
            companyId,
            ownerType,
            ownerId,
            role,
            cancellationToken
        );

    public Task<Stream?> OpenContentAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken = default
    ) => _fileStorage.GetAsync(mediaFile.StoragePath, cancellationToken);
}
