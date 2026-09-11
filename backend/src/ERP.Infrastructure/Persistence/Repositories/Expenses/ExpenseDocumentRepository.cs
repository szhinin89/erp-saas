using ERP.Application.Common;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Expenses;

public sealed class ExpenseDocumentRepository : IExpenseDocumentRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public ExpenseDocumentRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<ExpenseDocument> Scoped(Guid tenantId) =>
        _db.ExpenseDocuments.ForOperationalScope(tenantId, _company);

    public async Task<(IReadOnlyList<ExpenseDocument> Items, IReadOnlyDictionary<Guid, int> LineCounts, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid branchId,
        string? search = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 25,
        CancellationToken ct = default
    )
    {
        var query = Scoped(tenantId).Where(x => x.BranchId == branchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.DocumentNumber.Contains(s)
                || x.SupplierName.Contains(s)
                || x.SupplierTaxId.Contains(s)
            );
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ExpenseStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            query = query.Where(x => x.Status == parsedStatus);

        var total = await query.CountAsync(ct);
        var page = Math.Max(pageNumber, 1);
        var size = Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        var ids = items.Select(x => x.Id).ToArray();
        var lineCounts = ids.Length == 0
            ? new Dictionary<Guid, int>()
            : await _db.ExpenseLines
                .Where(x => ids.Contains(x.ExpenseDocumentId))
                .GroupBy(x => x.ExpenseDocumentId)
                .Select(x => new { DocumentId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.DocumentId, x => x.Count, ct);

        return (items, lineCounts, total);
    }

    public Task<ExpenseDocument?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Lines.OrderBy(l => l.SortOrder))
            .Include(x => x.PaymentSchedules.OrderBy(s => s.InstallmentNumber))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — mismo criterio ya cerrado para
    // PurchaseCreditNote/PurchaseInvoice: un gasto Cancelled es historial, no activo — nunca
    // cuenta como duplicado. Solo Draft/Confirmed cuenta como "activo".
    public Task<ExpenseDocument?> GetBySupplierAndDocumentNumberAsync(
        Guid tenantId,
        Guid supplierId,
        string documentType,
        string documentNumber,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .FirstOrDefaultAsync(
                x =>
                    x.SupplierId == supplierId
                    && x.DocumentType == documentType
                    && x.DocumentNumber == documentNumber
                    && x.Status != ExpenseStatus.Cancelled,
                ct
            );

    public Task<bool> ExistsByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .AnyAsync(x => x.AccessKey == accessKey && x.Status != ExpenseStatus.Cancelled, ct);

    public Task<bool> ExistsByReceptionDocumentIdAsync(Guid tenantId, Guid receptionDocumentId, CancellationToken ct = default) =>
        Scoped(tenantId)
            .AnyAsync(
                x => x.ReceptionDocumentId == receptionDocumentId && x.Status != ExpenseStatus.Cancelled,
                ct
            );

    /// <summary>
    /// RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — Id del gasto Cancelled más reciente con este
    /// AccessKey, si existe (nunca el activo) — para "Ver gasto anulado" (historial) en la UI de
    /// Recepción. Por AccessKey (no ReceptionDocumentId) porque es el mismo dato ya disponible en
    /// PurchaseReceptionVerifier al momento de importar, sin esperar a que exista el documento de
    /// recepción persistido — mismo criterio que <c>IPurchaseInvoiceRepository.GetLatestCancelledIdByAccessKeyAsync</c>.
    /// </summary>
    public Task<Guid?> GetLatestCancelledIdByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Where(x => x.AccessKey == accessKey && x.Status == ExpenseStatus.Cancelled)
            .OrderByDescending(x => x.CancelledAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public Task AddAsync(ExpenseDocument document, CancellationToken ct = default) =>
        _db.ExpenseDocuments.AddAsync(document, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
