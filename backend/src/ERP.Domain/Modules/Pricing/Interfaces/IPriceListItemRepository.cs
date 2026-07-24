using ERP.Domain.Modules.Pricing.Entities;

namespace ERP.Domain.Modules.Pricing.Interfaces;

public interface IPriceListItemRepository
{
    /// <summary>Todas las asignaciones del ítem (activas e inactivas) — permite reactivar en vez de duplicar.</summary>
    Task<IReadOnlyList<PriceListItem>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct = default);

    /// <summary>Asignaciones activas de una PriceList — dirección inversa de <see cref="GetByItemAsync"/>.</summary>
    Task<IReadOnlyList<PriceListItem>> GetByPriceListAsync(Guid tenantId, Guid priceListId, CancellationToken ct = default);

    /// <summary>
    /// Busca la asignación por su clave (PriceListId, ItemId) sin filtrar por IsActive — usada
    /// para validar la invariante "no puede existir una PricingRule activa sin una PriceListItem
    /// activa" antes de crear o reactivar una excepción de precio.
    /// </summary>
    Task<PriceListItem?> FindByKeyAsync(Guid tenantId, Guid priceListId, Guid itemId, CancellationToken ct = default);

    Task AddAsync(PriceListItem assignment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
