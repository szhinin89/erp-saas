using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;

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

    public Task<PurchasePayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.PurchasePayables
            .ForOperationalScope(tenantId, _company)
            .Include(x => x.Installments.OrderBy(i => i.InstallmentNumber))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
