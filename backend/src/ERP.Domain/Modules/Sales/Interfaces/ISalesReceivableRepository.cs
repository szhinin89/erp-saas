using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Modules.Sales.Interfaces;

public interface ISalesReceivableRepository
{
    Task<SalesReceivable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<SalesReceivable?> GetByInvoiceIdAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct = default
    );
    Task<(IReadOnlyList<SalesReceivable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task AddAsync(SalesReceivable receivable, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
