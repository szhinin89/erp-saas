using ERP.Application.Common;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class IdentificationUsageValidator : IIdentificationUsageValidator
{
    private readonly ErpDbContext _db;

    public IdentificationUsageValidator(ErpDbContext db) => _db = db;

    public async Task<bool> IsAllowedAsync(
        string idTypeCode,
        IdentificationUsageType usageType,
        CancellationToken ct = default
    ) =>
        await _db
            .SriIdTypeUsages.AsNoTracking()
            .AnyAsync(
                u => u.IdTypeCode == idTypeCode && u.UsageType == usageType && u.IsActive,
                ct
            );
}
