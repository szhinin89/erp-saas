using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

public sealed class PurchaseInvoiceRepository : IPurchaseInvoiceRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public PurchaseInvoiceRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<PurchaseInvoice> Scoped(Guid tenantId) =>
        _db.PurchaseInvoices.ForOperationalScope(tenantId, _company);

    public async Task<PurchaseInvoice?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    )
    {
        var invoice = await Scoped(tenantId)
            .Include(x => x.Lines.OrderBy(l => l.SortOrder))
                .ThenInclude(l => l.Taxes)
            .Include(x => x.PaymentSchedules.OrderBy(s => s.InstallmentNumber))
            .Include(x => x.TaxSummaries)
            .Include(x => x.CostDistributions).ThenInclude(d => d.Allocations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        invoice?.BindCostDistributions();
        return invoice;
    }

    // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — mismo criterio ya cerrado para
    // PurchaseCreditNote: una compra Cancelled es historial, no activa — nunca cuenta como
    // duplicado de AccessKey ni bloquea "Crear compra" desde la misma recepción. Solo
    // Draft/Confirmed cuenta como "activa".
    public Task<PurchaseInvoice?> GetByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .FirstOrDefaultAsync(
                x => x.AccessKey == accessKey
                    && (x.Status == PurchaseStatus.Draft || x.Status == PurchaseStatus.Confirmed),
                ct
            );

    /// <summary>
    /// RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — compra Cancelled/Discarded más reciente con este
    /// AccessKey, si existe, para que la UI de Recepción ofrezca el historial sin bloquear "Crear
    /// compra" de nuevo. El estado histórico se lee directamente del estado persistido.
    /// </summary>
    public Task<HistoricalPurchaseRef?> GetLatestHistoricalByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Where(x => x.AccessKey == accessKey
                && (x.Status == PurchaseStatus.Cancelled || x.Status == PurchaseStatus.Discarded))
            .OrderByDescending(x => x.CancelledAt)
            .Select(x => new HistoricalPurchaseRef(x.Id, x.Status))
            .FirstOrDefaultAsync(ct);

    // PURCHASE-CREDIT-NOTE-AFFECTED-INVOICE-RESOLVES-CANCELLED-01 — usado por
    // PurchaseReceptionVerifier para resolver la "factura afectada" que habilita "Procesar NC"
    // desde Recepción: una compra Cancelled es historial, nunca debe poder ser la factura afectada
    // de una NC nueva. Excluir Cancelled aquí es suficiente para desambiguar sin necesidad de un
    // criterio de desempate explícito — uq_purchase_invoices_tenant_company_supplier_number ya es
    // un índice único FILTRADO a Draft/Confirmed: a
    // lo sumo una fila no-Cancelled puede existir para el mismo (supplier, invoiceNumber), así que
    // nunca hay ambigüedad entre "cuál Confirmed elegir" — la que exista, si existe, ya es única.
    public Task<PurchaseInvoice?> GetBySupplierAndInvoiceNumberAsync(
        Guid tenantId,
        Guid supplierId,
        string invoiceNumber,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .FirstOrDefaultAsync(
                x =>
                    x.SupplierId == supplierId
                    && x.InvoiceNumber == invoiceNumber
                    && (x.Status == PurchaseStatus.Draft || x.Status == PurchaseStatus.Confirmed),
                ct
            );

    public async Task<IReadOnlyDictionary<Guid, string>> GetSupplierNamesByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> purchaseInvoiceIds,
        CancellationToken ct = default
    )
    {
        if (purchaseInvoiceIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await Scoped(tenantId)
            .Where(x => purchaseInvoiceIds.Contains(x.Id))
            .Select(x => new { x.Id, x.SupplierName })
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.SupplierName, ct);
    }

    public async Task<
        IReadOnlyDictionary<
            Guid,
            (string InvoiceNumber, string SupplierName, string Status, DateOnly IssueDate)
        >
    > GetJournalSourceSummariesByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> purchaseInvoiceIds,
        CancellationToken ct = default
    )
    {
        if (purchaseInvoiceIds.Count == 0)
            return new Dictionary<Guid, (string, string, string, DateOnly)>();

        var rows = await Scoped(tenantId)
            .AsNoTracking()
            .Where(x => purchaseInvoiceIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.SupplierName,
                x.Status,
                x.IssueDate,
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            x => x.Id,
            x => (x.InvoiceNumber, x.SupplierName, x.Status.ToString(), x.IssueDate)
        );
    }

    public async Task<(
        IReadOnlyList<PurchaseInvoice> Items,
        IReadOnlyDictionary<Guid, int> LineCounts,
        int Total
    )> GetPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        int page,
        int pageSize,
        Guid? supplierId = null,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId);

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<PurchaseStatus>(status.Trim(), true, out var ps)
        )
            q = q.Where(x => x.Status == ps);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // FASE 5 PURCHASE-UX — búsqueda por número de factura o por proveedor (razón social /
            // RUC del snapshot de la compra), sin distinguir mayúsculas.
            var pattern = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.InvoiceNumber, pattern)
                || EF.Functions.ILike(x.SupplierName, pattern)
                || EF.Functions.ILike(x.SupplierTaxId, pattern)
            );
        }

        if (supplierId is { } sid)
            q = q.Where(x => x.SupplierId == sid);

        var total = await q.CountAsync(ct);
        // FASE 5 PURCHASE-UX — líneas + impuestos de la página para exponer GrandTotal en el
        // listado (un borrador calcula su total desde las líneas; ICE vive en Taxes).
        var items = await q.Include(x => x.Lines)
            .ThenInclude(l => l.Taxes)
            .Include(x => x.CostDistributions).ThenInclude(d => d.Allocations)
            .AsSplitQuery()
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = items.Select(i => i.Id).ToList();
        var lineCounts = await _db.Set<PurchaseInvoiceDetail>()
            .Where(d => ids.Contains(d.InvoiceId))
            .GroupBy(d => d.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Count, ct);

        return (items, lineCounts, total);
    }

    public async Task<IReadOnlyList<PurchaseInvoice>> GetForSupplierReportAsync(
        Guid tenantId,
        DateOnly dateFrom,
        DateOnly dateTo,
        Guid? supplierId,
        CancellationToken ct = default
    )
    {
        var q = Scoped(tenantId).Where(x => x.IssueDate >= dateFrom && x.IssueDate <= dateTo);
        if (supplierId.HasValue)
            q = q.Where(x => x.SupplierId == supplierId.Value);

        return await q.OrderBy(x => x.IssueDate)
            .ThenBy(x => x.CreatedAt)
            .Include(x => x.Lines)
            .Include(x => x.CostDistributions).ThenInclude(d => d.Allocations)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlySet<Guid>> GetPackagingLevelIdsUsedInConfirmedDocumentsAsync(
        Guid tenantId,
        Guid itemId,
        IReadOnlyCollection<Guid> packagingLevelIds,
        CancellationToken ct = default
    )
    {
        if (packagingLevelIds.Count == 0)
            return new HashSet<Guid>();

        var ids = await (
            from line in _db.Set<PurchaseInvoiceDetail>()
            join invoice in Scoped(tenantId) on line.InvoiceId equals invoice.Id
            where
                line.TenantId == tenantId
                && line.ItemId == itemId
                && line.PackagingLevelId.HasValue
                && packagingLevelIds.Contains(line.PackagingLevelId.Value)
                && invoice.Status == PurchaseStatus.Confirmed
            select line.PackagingLevelId!.Value
        )
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    public Task AddAsync(PurchaseInvoice invoice, CancellationToken ct = default) =>
        _db.PurchaseInvoices.AddAsync(invoice, ct).AsTask();

    public async Task RemoveLinesByInvoiceAsync(
        Guid invoiceId,
        IEnumerable<PurchaseInvoiceDetail> newLines,
        CancellationToken ct = default
    )
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM purchase_cost_distributions WHERE purchase_invoice_id = {invoiceId}",
            ct
        );
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM purchase_invoice_details WHERE purchase_invoice_id = {invoiceId}",
            ct
        );

        foreach (var entry in _db.ChangeTracker.Entries<PurchaseCostDistributionAllocation>().ToList())
            entry.State = EntityState.Detached;
        foreach (var entry in _db.ChangeTracker.Entries<PurchaseCostDistribution>().ToList())
            entry.State = EntityState.Detached;
        foreach (var entry in _db.ChangeTracker.Entries<PurchaseInvoiceDetail>().ToList())
            entry.State = EntityState.Detached;

        foreach (var line in newLines)
            _db.Set<PurchaseInvoiceDetail>().Add(line);
    }

    public async Task ClearScheduleTrackingAsync(Guid invoiceId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM purchase_payment_schedules WHERE purchase_invoice_id = {invoiceId}",
            ct
        );

        foreach (var entry in _db.ChangeTracker.Entries<PurchasePaymentSchedule>().ToList())
            entry.State = EntityState.Detached;
    }

    public void ReattachSchedulesAsAdded(PurchaseInvoice invoice)
    {
        foreach (var entry in _db.ChangeTracker.Entries<PurchasePaymentSchedule>().ToList())
            entry.State = EntityState.Detached;

        foreach (var schedule in invoice.PaymentSchedules)
            _db.Set<PurchasePaymentSchedule>().Add(schedule);
    }

    public void ReattachCostDistributionsAsAdded(PurchaseInvoice invoice)
    {
        foreach (var distribution in invoice.CostDistributions)
            _db.Set<PurchaseCostDistribution>().Add(distribution);
    }

    public void TrackCommunication(PurchaseCommunication communication) =>
        _db.PurchaseCommunications.Add(communication);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
