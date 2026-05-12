using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ErpDbContext _db;

    public PasswordResetTokenRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(PasswordResetToken entity, CancellationToken ct = default)
        => _db.PasswordResetTokens.AddAsync(entity, ct).AsTask();

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task InvalidateActiveForUserAsync(Guid userId, string userKind, Guid? tenantId, CancellationToken ct = default)
    {
        var query = _db.PasswordResetTokens.Where(t => t.UserId == userId && t.UserKind == userKind && !t.Used);
        if (tenantId.HasValue)
            query = query.Where(t => t.TenantId == tenantId);
        else
            query = query.Where(t => t.TenantId == null);

        var rows = await query.ToListAsync(ct);
        foreach (var r in rows)
            r.MarkUsed();
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
