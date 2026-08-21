using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

/// <summary>
/// Fase 5.5.5.4 — carga de <c>PurchasePayable</c> por su propio Id (necesaria para aplicar/reversar
/// pagos). Separado de <c>PurchaseInvoiceRepository</c> (que ya implementa
/// <c>IPurchaseInvoiceRepository.GetPayableByPurchaseIdAsync</c>) — ambas implementaciones
/// comparten el mismo <see cref="ErpDbContext"/> y por lo tanto el mismo <c>ChangeTracker</c>.
/// </summary>
public sealed class PurchasePayableRepository : IPurchasePayableRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public PurchasePayableRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<PurchasePayable?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .PurchasePayables.ForOperationalScope(tenantId, _company)
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<(IReadOnlyList<PurchasePayable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    ) => GetPagedAsync(tenantId, status, null, page, pageSize, ct);

    public async Task<(IReadOnlyList<PurchasePayable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        Guid? supplierId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = _db.PurchasePayables.ForOperationalScope(tenantId, _company);

        // ERP-CORE-CLOSEOUT-08: PurchasePayable.Status nunca transiciona a "paid" (RegisterPayment
        // solo acumula PaidAmount) — filtrar por Status=="paid" literal nunca coincidía con
        // ninguna fila, dejando el filtro "Pagadas" siempre vacío. Mismo fix ya aplicado en
        // SalesReceivableRepository.GetPagedAsync (FINANCE-RECEIVABLES-LIST-ENTERPRISE-01):
        // "pending"/"paid" se traducen al saldo real (BalanceDue); "cancelled" y cualquier otro
        // valor futuro siguen siendo comparación literal de Status.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            q = normalized switch
            {
                "pending" => q.Where(x =>
                    x.Status != "cancelled"
                    && x.TotalAmount
                        - x.PaidAmount
                        - x.TotalRetained
                        - x.ReturnAppliedAmount
                        - x.SupplierCreditAppliedAmount
                        - x.CreditNoteAppliedAmount
                        > 0
                ),
                "paid" => q.Where(x =>
                    x.Status != "cancelled"
                    && x.TotalAmount
                        - x.PaidAmount
                        - x.TotalRetained
                        - x.ReturnAppliedAmount
                        - x.SupplierCreditAppliedAmount
                        - x.CreditNoteAppliedAmount
                        <= 0
                ),
                _ => q.Where(x => x.Status == normalized),
            };
        }
        if (supplierId is not null)
            q = q.Where(x => x.SupplierId == supplierId.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    /// <inheritdoc/>
    public Task<Guid?> GetPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .PurchasePayables.ForOperationalScope(tenantId, _company)
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (Guid?)x.PurchaseId)
            .FirstOrDefaultAsync(ct);
}
