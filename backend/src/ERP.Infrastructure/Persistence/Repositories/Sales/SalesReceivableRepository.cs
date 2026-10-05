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

    public async Task<IReadOnlyDictionary<Guid, SalesReceivable>> GetByIdsForUpdateAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    )
    {
        // Mismo patrón oficial que CashSessionRepository.GetByIdForUpdateAsync (lock → lectura
        // acotada → Reload). Orden ascendente por Id: dos cobros sobre las mismas CxC toman los
        // locks en el mismo orden y nunca se bloquean mutuamente.
        var result = new Dictionary<Guid, SalesReceivable>();
        foreach (var id in ids.Distinct().OrderBy(x => x))
        {
            if (_company.HasCompanyContext)
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM sales_receivables WHERE tenant_id = {tenantId} AND company_id = {_company.CompanyId} AND id = {id} FOR UPDATE",
                    ct
                );
            else
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM sales_receivables WHERE tenant_id = {tenantId} AND id = {id} FOR UPDATE",
                    ct
                );

            var receivable = await GetByIdAsync(tenantId, id, ct);
            if (receivable is null)
                continue;
            await _db.Entry(receivable).ReloadAsync(ct);
            result[id] = receivable;
        }
        return result;
    }

    public async Task<SalesReceivable?> GetByInvoiceIdForUpdateAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct = default
    )
    {
        var id = await Scoped(tenantId)
            .Where(x => x.InvoiceId == invoiceId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (id is null)
            return null;
        var locked = await GetByIdsForUpdateAsync(tenantId, [id.Value], ct);
        return locked.GetValueOrDefault(id.Value);
    }

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
