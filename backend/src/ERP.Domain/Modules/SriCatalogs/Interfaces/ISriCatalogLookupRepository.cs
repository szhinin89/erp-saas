using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Domain.Modules.SriCatalogs.Interfaces;

/// <summary>
/// Lecturas de catálogos globales de solo lectura (schema <c>global</c>, sin tenant/company scope)
/// consumidos por <c>CatalogController</c>. Sigue el mismo patrón de <see cref="SriUom"/> et al.:
/// tablas sin <c>TenantId</c>/<c>CompanyId</c>, PK=Code (o Id para algunos catálogos), seed vía HasData.
/// </summary>
public interface ISriCatalogLookupRepository
{
    Task<IReadOnlyList<SriUom>> GetActiveUomsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriVatRate>> GetActiveVatRatesAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriIceRate>> GetActiveIceRatesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriRetentionCode>> GetActiveRetentionCodesAsync(string? taxType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriTaxSupport>> GetActiveTaxSupportCodesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriDocType>> GetActiveDocTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriPaymentMethod>> GetActivePaymentMethodsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriSupplierType>> GetActiveSupplierTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriTaxRegime>> GetActiveTaxRegimesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonTypeCatalog>> GetPersonTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarcodeTypeDefinition>> GetActiveBarcodeTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemMarginStatusDefinition>> GetItemMarginStatusesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriIdType>> GetSriIdTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SriIdType>> GetSriIdTypesByUsageAsync(IdentificationUsageType usage, CancellationToken cancellationToken = default);
}
