using ERP.Domain.Products.Entities;

namespace ERP.Domain.Products.Interfaces;

public interface ITaxRateRepository
{
    Task<TaxRate?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
}

