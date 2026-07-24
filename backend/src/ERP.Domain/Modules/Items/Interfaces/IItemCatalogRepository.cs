using ERP.Domain.Modules.Items.Entities;

namespace ERP.Domain.Modules.Items.Interfaces;

public interface IItemCatalogRepository
{
    Task<Brand?>                GetBrandByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Brand>>  GetBrandsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool>                  BrandCodeExistsAsync(string code, Guid tenantId, CancellationToken cancellationToken = default);
    Task                        AddBrandAsync(Brand brand, CancellationToken cancellationToken = default);

    /// <summary>Verifica que el código exista y esté activo en el catálogo global barcode_types.</summary>
    Task<bool> BarcodeTypeExistsAndActiveAsync(string code, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
