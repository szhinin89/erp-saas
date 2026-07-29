using ERP.Domain.Modules.Media.Entities;
using ERP.Domain.Modules.Media.Enums;
using ERP.Domain.Modules.Media.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class MediaFileRepository : IMediaFileRepository
{
    private readonly ErpDbContext _db;

    public MediaFileRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task<MediaFile?> GetActivePrimaryAsync(
        Guid tenantId,
        Guid companyId,
        MediaOwnerType ownerType,
        Guid ownerId,
        string role,
        CancellationToken cancellationToken = default
    ) =>
        _db.MediaFiles.FirstOrDefaultAsync(
            m =>
                m.TenantId == tenantId
                && m.CompanyId == companyId
                && m.OwnerType == ownerType
                && m.OwnerId == ownerId
                && m.Role == role
                && m.IsPrimary
                && m.IsActive,
            cancellationToken
        );

    public Task AddAsync(MediaFile mediaFile, CancellationToken cancellationToken = default)
    {
        _db.MediaFiles.Add(mediaFile);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
