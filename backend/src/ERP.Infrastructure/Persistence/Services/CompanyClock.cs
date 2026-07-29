using ERP.Application.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación de <see cref="ICompanyClock"/>: resuelve <c>Company.Timezone</c> y convierte
/// <c>DateTime.UtcNow</c> a la hora local de la empresa antes de extraer la fecha calendario.
/// </summary>
public sealed class CompanyClock : ICompanyClock
{
    // Ecuador continental no observa horario de verano — desfase fijo UTC-5, usado solo si el
    // Timezone almacenado está vacío o el SO no reconoce el identificador (defensivo).
    private const string DefaultTimezoneId = "America/Guayaquil";

    private readonly ErpDbContext _db;

    public CompanyClock(ErpDbContext db) => _db = db;

    public async Task<DateOnly> TodayAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    )
    {
        var tz = await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return DateOnly.FromDateTime(localNow);
    }

    public async Task<(DateTime StartUtc, DateTime EndUtc)> TodayUtcRangeAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    )
    {
        var tz = await ResolveCompanyTimeZoneAsync(companyId, tenantId, ct);
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)
        );
        var localStart = localToday.ToDateTime(TimeOnly.MinValue);
        var localEnd = localToday.AddDays(1).ToDateTime(TimeOnly.MinValue);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, tz),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, tz)
        );
    }

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

        return ResolveTimeZone(timezoneId);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timezoneId)
    {
        var id = string.IsNullOrWhiteSpace(timezoneId) ? DefaultTimezoneId : timezoneId;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return FixedEcuadorOffset();
        }
        catch (InvalidTimeZoneException)
        {
            return FixedEcuadorOffset();
        }
    }

    private static TimeZoneInfo FixedEcuadorOffset() =>
        TimeZoneInfo.CreateCustomTimeZone(
            "Ecuador-Fixed-UTC-5",
            TimeSpan.FromHours(-5),
            "Ecuador (UTC-5)",
            "Ecuador (UTC-5)"
        );
}
