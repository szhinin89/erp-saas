using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ErpDbContext _db;

    public PasswordResetTokenRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(PasswordResetToken entity, CancellationToken cancellationToken = default)
        => _db.PasswordResetTokens.AddAsync(entity, cancellationToken).AsTask();

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task InvalidateActiveForUserAsync(Guid userId, string userKind, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var query = _db.PasswordResetTokens.Where(t => t.UserId == userId && t.UserKind == userKind && !t.Used);
        if (tenantId.HasValue)
            query = query.Where(t => t.TenantId == tenantId);
        else
            query = query.Where(t => t.TenantId == null);

        var rows = await query.ToListAsync(cancellationToken);
        foreach (var r in rows)
            r.MarkUsed();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
