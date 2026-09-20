using ERP.Domain.Modules.Pricing.Entities;

namespace ERP.Domain.Modules.Pricing.Interfaces;

public interface IPriceListCustomerRepository
{
    /// <summary>Todas las asignaciones del cliente (activas e inactivas) — permite reactivar en vez de duplicar.</summary>
    Task<IReadOnlyList<PriceListCustomer>> GetByCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default
    );

    /// <summary>Asignaciones activas de una PriceList — dirección inversa de <see cref="GetByCustomerAsync"/>.</summary>
    Task<IReadOnlyList<PriceListCustomer>> GetByPriceListAsync(
        Guid tenantId,
        Guid priceListId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Busca la asignación por su clave (PriceListId, CustomerId) sin filtrar por IsActive —
    /// usada para reactivar una relación existente en vez de duplicarla.
    /// </summary>
    Task<PriceListCustomer?> FindByKeyAsync(
        Guid tenantId,
        Guid priceListId,
        Guid customerId,
        CancellationToken ct = default
    );

    Task AddAsync(PriceListCustomer assignment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
