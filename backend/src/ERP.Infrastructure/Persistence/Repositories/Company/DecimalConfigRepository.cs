using ERP.Application.Modules.Companies.UseCases.DecimalConfig;
using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.CompanyConfig;

public sealed class DecimalConfigRepository : IDecimalConfigRepository
{
    private const string KeySalesPrice = "decimal.sales.unitPrice";
    private const string KeyPurchasePrice = "decimal.purchases.unitPrice";
    private const string KeyQuantity = "decimal.quantity";
    private const string KeyPercentage = "decimal.percentage";
    private const string KeyTotalAmount = "decimal.totalAmount";

    private const int DefaultSalesPrice = 2;
    private const int DefaultPurchasePrice = 4;
    private const int DefaultQuantity = 4;
    private const int DefaultPercentage = 2;
    private const int DefaultTotalAmount = 2;

    private readonly ErpDbContext _db;
    public DecimalConfigRepository(ErpDbContext db) => _db = db;

    public async Task<DecimalConfigDto> GetAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        var pars = await _db.GeneralParameters
            .Where(p => p.TenantId == tenantId && p.CompanyId == companyId
                     && p.Key.StartsWith("decimal."))
            .AsNoTracking()
            .ToListAsync(ct);

        return new DecimalConfigDto(
            GetInt(pars, KeySalesPrice, DefaultSalesPrice),
            GetInt(pars, KeyPurchasePrice, DefaultPurchasePrice),
            GetInt(pars, KeyQuantity, DefaultQuantity),
            GetInt(pars, KeyPercentage, DefaultPercentage),
            GetInt(pars, KeyTotalAmount, DefaultTotalAmount));
    }

    public async Task SaveAsync(Guid tenantId, Guid companyId,
        int salesUnitPrice, int purchaseUnitPrice, int quantity,
        int percentage, int totalAmount, CancellationToken ct = default)
    {
        var pars = await _db.GeneralParameters
            .Where(p => p.TenantId == tenantId && p.CompanyId == companyId
                     && p.Key.StartsWith("decimal."))
            .ToListAsync(ct);

        Upsert(pars, tenantId, companyId, KeySalesPrice, Clamp(salesUnitPrice));
        Upsert(pars, tenantId, companyId, KeyPurchasePrice, Clamp(purchaseUnitPrice));
        Upsert(pars, tenantId, companyId, KeyQuantity, Clamp(quantity));
        Upsert(pars, tenantId, companyId, KeyPercentage, Clamp(percentage));
        Upsert(pars, tenantId, companyId, KeyTotalAmount, Clamp(totalAmount));

        await _db.SaveChangesAsync(ct);
    }

    private static int GetInt(List<GeneralParameter> pars, string key, int defaultVal)
    {
        var p = pars.Find(x => x.Key == key);
        return p?.Value is not null && int.TryParse(p.Value, out var v) ? Clamp(v) : defaultVal;
    }

    private static int Clamp(int v) => Math.Clamp(v, 0, 6);

    private void Upsert(List<GeneralParameter> pars, Guid tenantId, Guid companyId, string key, int val)
    {
        var existing = pars.Find(x => x.Key == key);
        if (existing is not null)
        {
            existing.Value = val.ToString();
        }
        else
        {
            _db.GeneralParameters.Add(new GeneralParameter
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyId = companyId,
                Key = key,
                Value = val.ToString(),
                Description = key switch
                {
                    KeySalesPrice => "Decimales precio unitario ventas",
                    KeyPurchasePrice => "Decimales precio unitario compras",
                    KeyQuantity => "Decimales cantidad",
                    KeyPercentage => "Decimales porcentaje",
                    KeyTotalAmount => "Decimales total",
                    _ => null,
                },
            });
        }
    }
}
