using ERP.Application.Common.Services;

namespace ERP.API.Tests.Integration;

/// <summary>
/// DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01 — doble de prueba compartido por las suites end-to-end de
/// Integration que ya no llaman <c>DateTime.UtcNow</c> directamente. Fecha fija arbitraria — estas
/// suites no ejercitan comportamiento sensible a la fecha, solo necesitan una respuesta
/// determinista.
/// </summary>
internal sealed class AlwaysTodayCompanyClock : ICompanyClock
{
    private static readonly DateOnly FixedToday = new(2026, 9, 17);

    public Task<DateOnly> TodayAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    ) => Task.FromResult(FixedToday);

    public Task<(DateTime StartUtc, DateTime EndUtc)> TodayUtcRangeAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            (
                FixedToday.ToDateTime(TimeOnly.MinValue),
                FixedToday.AddDays(1).ToDateTime(TimeOnly.MinValue).AddTicks(-1)
            )
        );

    public Task<DateOnly> LocalDateAsync(
        Guid companyId,
        Guid tenantId,
        DateTime utcInstant,
        CancellationToken ct = default
    ) => Task.FromResult(DateOnly.FromDateTime(utcInstant));
}
