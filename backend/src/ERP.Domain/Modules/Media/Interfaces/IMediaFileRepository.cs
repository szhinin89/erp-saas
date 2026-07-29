using ERP.Domain.Modules.Media.Entities;
using ERP.Domain.Modules.Media.Enums;

namespace ERP.Domain.Modules.Media.Interfaces;

public interface IMediaFileRepository
{
    /// <summary>
    /// Busca el archivo activo y principal (<c>IsPrimary=true</c>, <c>IsActive=true</c>)
    /// para un propietario y rol determinados.
    /// </summary>
    Task<MediaFile?> GetActivePrimaryAsync(
        Guid tenantId,
        Guid companyId,
        MediaOwnerType ownerType,
        Guid ownerId,
        string role,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(MediaFile mediaFile, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
