using ERP.Domain.Modules.Pricing.Entities;

namespace ERP.Domain.Modules.Pricing.Interfaces;

public interface IPriceListRepository
{
    /// <summary>Carga completa: lista + entries + discounts.</summary>
    Task<PriceList?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lista de precios marcada como IsDefault=true para la company activa.</summary>
    Task<PriceList?> GetDefaultAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PriceList>> GetAllByCompanyAsync(CancellationToken ct = default);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Retorna true si ya existe otra lista con IsDefault=true en la misma company.</summary>
    Task<bool> HasDefaultAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las entradas de precio para un ítem específico
    /// en todas las listas activas de la company.
    /// </summary>
    Task<IReadOnlyList<PriceListEntry>> GetEntriesByItemAsync(
        Guid itemId, Guid? variantId, CancellationToken ct = default);

    Task AddAsync(PriceList priceList, CancellationToken ct = default);
    Task TrackEntryAsync(PriceListEntry entry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
