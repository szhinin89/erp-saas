using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.SriCatalogs;

/// <summary>
/// Implementación de <see cref="ISriCatalogLookupRepository"/> — consulta directa a las tablas
/// <c>global.sri_*</c> (y catálogos globales equivalentes de otros módulos). Sin tenant scope:
/// los datos son globales e inmutables, igual que <c>SriGlobalRateReader</c>.
/// </summary>
public sealed class SriCatalogLookupRepository : ISriCatalogLookupRepository
{
    private readonly ErpDbContext _db;

    public SriCatalogLookupRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SriUom>> GetActiveUomsAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriUoms.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriVatRate>> GetActiveVatRatesAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriVatRates.AsNoTracking()
            .Where(r =>
                r.IsActive
                && (r.ValidFrom == null || r.ValidFrom <= asOfDate)
                && (r.ValidUntil == null || r.ValidUntil >= asOfDate)
            )
            .OrderBy(r => r.Percentage)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriIceRate>> GetActiveIceRatesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriIceRates.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriRetentionCode>> GetActiveRetentionCodesAsync(
        string? taxType,
        CancellationToken cancellationToken = default
    )
    {
        var query = _db.SriRetentionCodes.AsNoTracking().Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(taxType))
            query = query.Where(r => r.TaxType == taxType.Trim().ToUpperInvariant());

        return await query
            .OrderBy(r => r.TaxType)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SriTaxSupport>> GetActiveTaxSupportCodesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriTaxSupports.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriDocType>> GetActiveDocTypesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriDocTypes.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriPaymentMethod>> GetActivePaymentMethodsAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriPaymentMethods.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriSupplierType>> GetActiveSupplierTypesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriSupplierTypes.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriTaxRegime>> GetActiveTaxRegimesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriTaxRegimes.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PersonTypeCatalog>> GetPersonTypesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .PersonTypeCatalogs.AsNoTracking()
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BarcodeTypeDefinition>> GetActiveBarcodeTypesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .BarcodeTypes.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemMarginStatusDefinition>> GetItemMarginStatusesAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .ItemMarginStatuses.AsNoTracking()
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriIdType>> GetSriIdTypesAsync(
        CancellationToken cancellationToken = default
    ) => await _db.SriIdTypes.AsNoTracking().OrderBy(t => t.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SriIdType>> GetSriIdTypesByUsageAsync(
        IdentificationUsageType usage,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SriIdTypeUsages.AsNoTracking()
            .Where(u => u.UsageType == usage && u.IsActive)
            .Join(
                _db.SriIdTypes.AsNoTracking(),
                u => u.IdTypeCode,
                t => t.Code,
                (u, t) =>
                    new SriIdType
                    {
                        Code = t.Code,
                        Name = t.Name,
                        Digits = t.Digits,
                    }
            )
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
}
