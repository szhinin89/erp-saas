using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación de ISriGlobalRateReader — consulta directa a global.sri_* tables.
/// Sin tenant scope: los datos son globales e inmutables.
/// </summary>
public sealed class SriGlobalRateReader : ISriGlobalRateReader
{
    private readonly ErpDbContext _db;

    public SriGlobalRateReader(ErpDbContext db) => _db = db;

    public async Task<decimal?> GetVatPercentageAsync(string code, CancellationToken ct = default)
    {
        var rate = await _db.SriVatRates.AsNoTracking()
            .Where(r => r.Code == code && r.IsActive)
            .Select(r => (decimal?)r.Percentage)
            .FirstOrDefaultAsync(ct);
        return rate;
    }

    public async Task<decimal?> GetIcePercentageAsync(string code, CancellationToken ct = default)
    {
        var rate = await _db.SriIceRates.AsNoTracking()
            .Where(r => r.Code == code && r.IsActive)
            .Select(r => r.Percentage)
            .FirstOrDefaultAsync(ct);
        return rate;
    }

    public async Task<decimal?> GetRetentionPercentageAsync(string code, CancellationToken ct = default)
    {
        var rate = await _db.SriRetentionCodes.AsNoTracking()
            .Where(r => r.Code == code && r.IsActive)
            .Select(r => (decimal?)r.Percentage)
            .FirstOrDefaultAsync(ct);
        return rate;
    }
}
