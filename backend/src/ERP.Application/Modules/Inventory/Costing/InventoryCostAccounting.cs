using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Events;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Costing;

public sealed class InventoryCostAccounting(IInventoryCostLedger ledger, IPostingEngine engine, IMediator mediator)
    : INotificationHandler<InventoryCostPostingRequestedEvent>
{
    public async Task RecordAsync(Guid tenantId, Guid companyId, Guid invoiceId, Guid sourceId,
        decimal amount, DateOnly date, string kind, Guid actorId, CancellationToken ct)
    {
        await ledger.LockInvoiceAsync(tenantId, companyId, invoiceId, ct);
        var existing = await ledger.GetInvoicePostingsAsync(tenantId, companyId, invoiceId, ct);
        if (kind == "Return")
        {
            var target = await ledger.GetCurrentInvoiceCostAsync(tenantId, companyId, invoiceId, ct);
            if (target.HasValue)
            {
                // A2 returns already staged one auditable cost record per movement, preserving
                // monetary rounding remainders. Legacy documents retain their aggregate path.
                await ProcessAsync(tenantId, companyId, invoiceId, ct);
                return;
            }
        }
        if (amount != 0m && !existing.Any(p => p.SourceEventId == sourceId && p.Kind == kind))
            ledger.Add(InventoryCostPosting.Create(tenantId, companyId, invoiceId, sourceId, amount, date, kind, actorId));
        await ProcessAsync(tenantId, companyId, invoiceId, ct);
    }

    public Task Handle(InventoryCostPostingRequestedEvent e, CancellationToken ct) =>
        ProcessAsync(e.TenantId!.Value, e.CompanyId, e.InvoiceId, ct);

    public async Task ProcessAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        await ledger.LockInvoiceAsync(tenantId, companyId, invoiceId, ct);
        var rows = await ledger.GetInvoicePostingsAsync(tenantId, companyId, invoiceId, ct);
        foreach (var row in rows.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id))
        {
            if (row.Status is "Posted" or "Canceled") continue;
            if (row.Amount == 0m) { row.Posted(null); continue; }
            var fact = new PostingFact(tenantId, companyId, "Sales", row.FactType, row.SourceEventId,
                row.EntryDate, 0m, 0m, 0m, 0m, 0m, HistoricalCostTotal: row.Amount);
            var result = await engine.PostAsync(fact, ct);
            if (!result.IsSuccess)
            {
                row.Failed(result.Code, result.Error);
                break; // Later adjustments/returns cannot precede their recognized original cost.
            }
            row.Posted(result.Value!.JournalEntryId);
        }
    }

    public async Task CancelAsync(Guid tenantId, Guid companyId, Guid invoiceId, string reason, CancellationToken ct)
    {
        await ledger.LockInvoiceAsync(tenantId, companyId, invoiceId, ct);
        var rows = await ledger.GetInvoicePostingsAsync(tenantId, companyId, invoiceId, ct);
        foreach (var row in rows.Where(p => p.Status != "Canceled"))
        {
            if (row.JournalEntryId.HasValue)
            {
                var result = await mediator.Send(new ReverseJournalEntryCommand(row.JournalEntryId.Value, reason), ct);
                if (!result.IsSuccess)
                    throw new DomainRuleViolationException($"No se pudo reversar el costo de la venta: {result.Error}");
            }
            row.Cancel();
        }
    }
}
