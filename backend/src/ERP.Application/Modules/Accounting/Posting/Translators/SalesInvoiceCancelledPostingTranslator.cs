using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Anulación de factura de venta: por cada hecho contable de la factura (ingreso y cada obligación de
/// costo: reconocimiento, coberturas y devoluciones) aplica la anulación común — asiento publicado →
/// reverso espejo con la fecha efectiva del acto; Pending/Failed sin asiento → Canceled
/// (ACCOUNTING-CONTINUITY-05, accounting.md §7 y §18). Solo la falta de período Open bloquea.
/// </summary>
public sealed class SalesInvoiceCancelledPostingTranslator(
    IAccountingReversalRecorder reversals,
    IAccountingPostingRepository postings)
    : INotificationHandler<SalesInvoiceCancelledEvent>
{
    public async Task Handle(SalesInvoiceCancelledEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var reason = $"Factura {e.InvoiceNumber} anulada: {e.CancelReason}";
        // El costo de facturas anteriores a la obligación durable puede tener asiento sin obligación:
        // su identidad (CostOfGoodsSold, factura) se resuelve igual contra el libro.
        var facts = new List<(string FactType, Guid SourceId)>
        {
            ("InvoiceIssued", e.InvoiceId),
            (SaleCostPostingRecorder.CostFactType, e.InvoiceId),
        };
        var costs = await postings.ListBySourceDocumentAsync(tenantId, e.CompanyId, e.InvoiceId, ct);
        facts.AddRange(costs
            .Where(p => SaleCostPostingRecorder.IsCostFact(p.SourceModule, p.FactType))
            // Ajuste resuelto en cero (Posted sin asiento): no hay efecto publicado que reversar.
            .Where(p => !(p.Status == AccountingPostingStatus.Posted && p.JournalEntryId is null))
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => (p.FactType, p.SourceEventId)));

        foreach (var (factType, sourceId) in facts.Distinct())
            await reversals.RecordSourceAsync(tenantId, e.CompanyId, SaleCostPostingRecorder.SourceModule,
                factType, sourceId, reason, e.OccurredOn, ct);
    }
}
