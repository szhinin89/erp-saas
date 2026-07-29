using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Domain.Modules.Media.Entities;
using ERP.Domain.Modules.Media.Enums;

namespace ERP.Application.Modules.Media;

/// <summary>
/// Petición para reemplazar el archivo principal (<c>IsPrimary=true</c>) de un
/// propietario + rol. El archivo activo anterior (si existe) se deshabilita
/// (no se borra físicamente) y se crea un nuevo <see cref="MediaFile"/>.
/// </summary>
public sealed record ReplacePrimaryMediaRequest(
    Guid TenantId,
    Guid CompanyId,
    MediaOwnerType OwnerType,
    Guid OwnerId,
    string Role,
    MediaType MediaType,
    MediaVisibility Visibility,
    MediaUploadContent Content,
    Guid UserId
);

/// <summary>
/// Fachada de aplicación del módulo Media. Otros módulos (p.ej. Companies)
/// orquestan subidas de archivos a través de esta interfaz, sin conocer
/// detalles de almacenamiento ni de persistencia de <see cref="MediaFile"/>.
/// </summary>
public interface IMediaService
{
    Task<Result<MediaFile>> ReplacePrimaryImageAsync(
        ReplacePrimaryMediaRequest request,
        CancellationToken cancellationToken = default
    );

    Task<MediaFile?> GetActivePrimaryAsync(
        Guid tenantId,
        Guid companyId,
        MediaOwnerType ownerType,
        Guid ownerId,
        string role,
        CancellationToken cancellationToken = default
    );

    Task<Stream?> OpenContentAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken = default
    );
}
