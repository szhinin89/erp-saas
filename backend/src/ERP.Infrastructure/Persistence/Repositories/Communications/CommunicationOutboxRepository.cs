using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Communications;

public sealed class CommunicationOutboxRepository : ICommunicationOutboxRepository
{
    private readonly ErpDbContext _db;

    public CommunicationOutboxRepository(ErpDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(CommunicationOutbox communication, CancellationToken ct = default) =>
        await _db.CommunicationOutbox.AddAsync(communication, ct);

    public Task<CommunicationOutbox?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CommunicationOutbox.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CommunicationOutbox?> GetByIdempotencyKeyAsync(
        Guid tenantId,
        Guid companyId,
        string idempotencyKey,
        CancellationToken ct = default
    ) =>
        _db.CommunicationOutbox.FirstOrDefaultAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.IdempotencyKey == idempotencyKey.Trim(),
            ct
        );
}
