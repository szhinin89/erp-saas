using ERP.Domain.Modules.Pricing.Entities;

namespace ERP.Domain.Modules.Pricing.Interfaces;

public interface IPricingRuleRepository
{
    Task<PricingRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PricingRule>> GetByPriceListAsync(Guid tenantId, Guid priceListId, CancellationToken ct = default);
    Task<IReadOnlyList<PricingRule>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct = default);

    /// <summary>Regla activa para un ítem dentro de una lista específica — usada por PricingResolver. A lo sumo una fila (índice único por lista+ítem).</summary>
    Task<PricingRule?> GetActiveForItemInListAsync(
        Guid tenantId, Guid priceListId, Guid itemId, CancellationToken ct = default);

    /// <summary>
    /// Busca por la clave única (PriceListId, ItemId) SIN filtrar por IsActive —
    /// a diferencia de <see cref="GetActiveForItemInListAsync"/>. Necesario para distinguir
    /// "no existe" de "existe pero está deshabilitada" antes de decidir crear vs. reactivar
    /// (el índice único de BD no distingue activo/inactivo — ver PricingRuleConfiguration).
    /// </summary>
    Task<PricingRule?> FindByKeyAsync(
        Guid tenantId, Guid priceListId, Guid itemId, CancellationToken ct = default);

    Task AddAsync(PricingRule rule, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
