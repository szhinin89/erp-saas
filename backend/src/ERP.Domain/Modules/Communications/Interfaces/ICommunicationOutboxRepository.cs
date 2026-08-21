using ERP.Domain.Modules.Communications.Entities;

namespace ERP.Domain.Modules.Communications.Interfaces;

public interface ICommunicationOutboxRepository
{
    Task AddAsync(CommunicationOutbox communication, CancellationToken ct = default);

    Task<CommunicationOutbox?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CommunicationOutbox?> GetByIdempotencyKeyAsync(
        Guid tenantId,
        Guid companyId,
        string idempotencyKey,
        CancellationToken ct = default
    );
}
