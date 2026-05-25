using ERP.Domain.Modules.Fiscal.Entities;

namespace ERP.Domain.Modules.Fiscal.Interfaces;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetPagedAsync(
        Guid subscriberId,
        int pageNumber,
        int pageSize,
        Guid? businessPartnerId,
        DateTime? from,
        DateTime? to,
        string? status,
        string? search,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceRetryCandidate>> ListPendingElectronicRetryAsync(CancellationToken ct = default);
    Task<Guid?> GetPublicIdByInternalIdAsync(long id, Guid subscriberId, CancellationToken ct = default);
}

public sealed record InvoiceRetryCandidate(Guid PublicId, Guid SubscriberId);
