using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Finance;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly ErpDbContext _context;

    public PaymentRepository(ErpDbContext context)
    {
        _context = context;
    }

    public Task<Payment?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _context
            .Payments.Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<
        IReadOnlyDictionary<Guid, (Guid PartnerId, decimal Amount, DateOnly PaymentDate, string? Reference, string Status)>
    > GetJournalSourceSummariesByIdsAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    )
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, (Guid, decimal, DateOnly, string?, string)>();

        var rows = await _context
            .Payments.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.PartnerId,
                x.Amount,
                x.PaymentDate,
                x.Reference,
                x.Status,
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            x => x.Id,
            x => (x.PartnerId, x.Amount, x.PaymentDate, x.Reference, x.Status.ToString())
        );
    }

    public Task AddAsync(Payment payment, CancellationToken ct = default) =>
        _context.Payments.AddAsync(payment, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
