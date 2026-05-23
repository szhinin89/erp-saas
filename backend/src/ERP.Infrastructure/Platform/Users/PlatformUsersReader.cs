using ERP.Application.Platform.Users;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using Microsoft.EntityFrameworkCore;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Platform.Users;

public sealed class PlatformUsersReader : IPlatformUsersReader
{
    private readonly ErpDbContext _db;

    public PlatformUsersReader(ErpDbContext db) => _db = db;

    public async Task<PlatformUsersPage> ListAsync(CancellationToken ct = default)
    {
        var entities = await _db.IdentityUsers
            .AsNoTracking()
            .Where(u => u.UserType == IdentityUserType.Platform)
            .OrderBy(u => u.EmailNormalized)
            .ToListAsync(ct);

        var rows = entities
            .Select(u => new PlatformUserListItem(
                u.Id,
                u.Email.Value,
                u.FirstName,
                u.LastName,
                u.PlatformRole?.ToString(),
                u.IsActive))
            .ToList();

        return new PlatformUsersPage(rows, rows.Count(u => u.IsActive));
    }
}
