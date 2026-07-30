using ERP.Domain.MasterData.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class LegalEntityTypeRepository : ILegalEntityTypeRepository
{
    private readonly ErpDbContext _db;

    public LegalEntityTypeRepository(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<bool> ExistsActiveAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        return await _db.LegalEntityTypeCatalog
            .AnyAsync(
                x => x.Code == code && x.IsActive,
                cancellationToken
            );
    }
}
