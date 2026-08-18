using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

public sealed class SalesReceivableRepository : ISalesReceivableRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public SalesReceivableRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<SalesReceivable> Scoped(Guid tenantId) =>
        _db.SalesReceivables.ForOperationalScope(tenantId, _company);

    public Task<SalesReceivable?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SalesReceivable?> GetByInvoiceIdAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);

    public async Task<(IReadOnlyList<SalesReceivable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId);

        // FINANCE-RECEIVABLES-LIST-ENTERPRISE-01: SalesReceivable.Status nunca transiciona a
        // "paid" (RegisterCollection solo acumula PaidAmount) — el saldo en cero es la única
        // señal real de "pagada", igual que en PurchasePayable. Filtrar por Status=="pending"
        // literal dejaba pasar filas ya saldadas al filtro "Pendientes". "pending"/"paid" se
        // traducen aquí a la condición real de saldo; "cancelled" (y cualquier otro valor futuro)
        // sigue siendo comparación literal de Status.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            q = normalized switch
            {
                "pending" => q.Where(x =>
                    x.Status != "cancelled" && x.OriginalAmount - x.PaidAmount > 0
                ),
                "paid" => q.Where(x =>
                    x.Status != "cancelled" && x.OriginalAmount - x.PaidAmount <= 0
                ),
                _ => q.Where(x => x.Status == normalized),
            };
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(SalesReceivable receivable, CancellationToken ct = default) =>
        await _db.SalesReceivables.AddAsync(receivable, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
