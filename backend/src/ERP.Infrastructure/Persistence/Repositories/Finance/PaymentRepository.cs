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

    public Task<Payment?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default)
        => _context.Payments
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id)
            .FirstOrDefaultAsync(ct);

    public Task AddAsync(Payment payment, CancellationToken ct = default)
        => _context.Payments.AddAsync(payment, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
