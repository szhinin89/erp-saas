using ERP.Application.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación de <see cref="ICompanyClock"/>: resuelve <c>Company.Timezone</c> y delega la
/// aritmética de zona horaria en <see cref="CompanyTimeZone"/> (única implementación).
/// El instante "ahora" sale del <see cref="TimeProvider"/> registrado (producción:
/// <see cref="TimeProvider.System"/>) — ZH-ACCOUNTING-DATE-BOUNDARY-01: permite probar de forma
/// determinista la frontera entre el día UTC y el día de la empresa sin depender de la hora real.
/// </summary>
public sealed class CompanyClock : ICompanyClock
{
    private readonly ErpDbContext _db;
    private readonly TimeProvider _time;

    public CompanyClock(ErpDbContext db)
        : this(db, TimeProvider.System) { }

    public CompanyClock(ErpDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public async Task<DateOnly> TodayAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    ) => CompanyTimeZone.LocalDate(UtcNow, await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct));

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
        return CompanyTimeZone.DayUtcRange(CompanyTimeZone.LocalDate(UtcNow, tz), tz);
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
