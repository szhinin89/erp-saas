using ERP.Domain.Modules.Inventory.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — cobertura de costo pendiente o devolución valorizada por el Kardex:
/// registra la obligación del delta de costo (identidad = asignación o movimiento) anclada a la factura.
/// </summary>
public sealed class SaleCostRecognizedPostingTranslator(SaleCostPostingRecorder costs)
    : INotificationHandler<SaleCostRecognizedEvent>
{
    public Task Handle(SaleCostRecognizedEvent e, CancellationToken ct) =>
        costs.RecordForInvoiceAsync(e.TenantId!.Value, e.CompanyId, e.InvoiceId, e.SourceEventId,
            e.Amount, e.EntryDate, ct, e.RecognizedCostTarget);
}
