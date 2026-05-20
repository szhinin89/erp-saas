using ERP.Domain.Subscribers.Entities;

namespace ERP.Domain.Subscribers.Interfaces;

public interface ISubscriberRepository
{
    Task<Subscriber?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Subscriber?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Subscriber>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Subscriber tenant, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
