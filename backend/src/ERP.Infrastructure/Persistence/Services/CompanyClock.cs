using ERP.Application.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación de <see cref="ICompanyClock"/>: resuelve <c>Company.Timezone</c> y delega la
/// aritmética de zona horaria en <see cref="CompanyTimeZone"/> (única implementación).
/// </summary>
public sealed class CompanyClock : ICompanyClock
{
    private readonly ErpDbContext _db;

    public CompanyClock(ErpDbContext db) => _db = db;

    public async Task<DateOnly> TodayAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    ) => CompanyTimeZone.Today(await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct));

    public async Task<DateOnly> LocalDateAsync(
        Guid companyId,
        Guid tenantId,
        DateTime utcInstant,
        CancellationToken ct = default
    ) =>
        CompanyTimeZone.LocalDate(
            utcInstant,
            await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct)
        );

    public async Task<(DateTime StartUtc, DateTime EndUtc)> TodayUtcRangeAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    )
    {
        var tz = await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct);
        return CompanyTimeZone.DayUtcRange(CompanyTimeZone.Today(tz), tz);
    }

    public async Task<(DateTime StartUtc, DateTime EndUtc)> DayUtcRangeAsync(
        Guid companyId,
        Guid tenantId,
        DateOnly day,
        CancellationToken ct = default
    ) =>
        CompanyTimeZone.DayUtcRange(day, await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct));

    public async Task<DateTime> CompanyLocalToUtcAsync(
        Guid companyId,
        Guid tenantId,
        DateTime companyLocal,
        CancellationToken ct = default
    ) =>
        CompanyTimeZone.ToUtc(
            companyLocal,
            await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct)
        );

    private async Task<TimeZoneInfo> ResolveCompanyTimeZoneAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct
    )
    {
        var timezoneId = await _db
            .Companies.AsNoTracking()
            .Where(c => c.Id == companyId && c.TenantId == tenantId)
            .Select(c => c.Timezone)
            .FirstOrDefaultAsync(ct);

        return CompanyTimeZone.Resolve(timezoneId);
    }
}
