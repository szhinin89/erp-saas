using ERP.Application.Common.Services;
using Microsoft.EntityFrameworkCore;
using PurchasesTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Infrastructure.Persistence.Services;

public sealed class SriTaxResolver : ISriTaxResolver, PurchasesTaxResolver
{
    private readonly ErpDbContext _db;

    public SriTaxResolver(ErpDbContext db) => _db = db;

    public async Task<decimal?> GetVatRateAsync(string vatCode, CancellationToken ct = default)
    {
        var rate = await _db
            .SriVatRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == vatCode && r.IsActive, ct);
        return rate?.Percentage;
    }

    public async Task<decimal?> GetIceRateAsync(string iceCode, CancellationToken ct = default)
    {
        var rate = await _db
            .SriIceRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == iceCode && r.IsActive, ct);
        return rate?.Percentage;
    }

    public async Task<TaxRateResult?> GetVatRateWithNameAsync(
        string vatCode,
        CancellationToken ct = default
    )
    {
        var rate = await _db
            .SriVatRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == vatCode && r.IsActive, ct);
        return rate is null ? null : new TaxRateResult(rate.Percentage, rate.Name);
    }

    public async Task<TaxRateResult?> GetIceRateWithNameAsync(
        string iceCode,
        CancellationToken ct = default
    )
    {
        var rate = await _db
            .SriIceRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == iceCode && r.IsActive, ct);
        return rate?.Percentage is null
            ? null
            : new TaxRateResult(rate.Percentage.Value, rate.Name);
    }

    public async Task<string?> GetPaymentMethodNameAsync(
        string code,
        CancellationToken ct = default
    )
    {
        var pm = await _db
            .SriPaymentMethods.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == code && m.IsActive, ct);
        return pm?.Name;
    }
}
