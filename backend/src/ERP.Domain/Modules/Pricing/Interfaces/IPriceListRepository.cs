using ERP.Domain.Modules.Pricing.Entities;

namespace ERP.Domain.Modules.Pricing.Interfaces;

public interface IPriceListRepository
{
    Task<PriceList?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PriceList>> GetAllAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default
    );
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? excludeId,
        CancellationToken ct = default
    );
    Task<bool> DefaultExistsAsync(Guid tenantId, Guid? excludeId, CancellationToken ct = default);

    /// <summary>Lista predeterminada activa vigente del tenant, o null si no hay ninguna — usado para reasignar el default desde Configuración → Ventas.</summary>
    Task<PriceList?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Toda lista de la empresa operativa con <c>IsDefault = true</c>, activa o no — el índice
    /// <c>uq_price_lists_tenant_company_default</c> no filtra por estado. Uso exclusivo de
    /// <see cref="IPriceListDefaultAssigner"/>; la resolución de precios usa <see cref="GetDefaultAsync"/>.
    /// </summary>
    Task<IReadOnlyList<PriceList>> GetDefaultFlagHoldersAsync(
        Guid tenantId,
        CancellationToken ct = default
    );
    Task AddAsync(PriceList priceList, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
