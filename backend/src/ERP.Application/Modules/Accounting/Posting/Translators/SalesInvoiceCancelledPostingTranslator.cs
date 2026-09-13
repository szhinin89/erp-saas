using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — mismo criterio
/// arquitectónico ya fijado en ACCOUNTING-REVERSALS-05 (ver <see cref="PurchaseInvoiceCancelledPostingTranslator"/>,
/// Compras): anular una factura de venta ya contabilizada deshace ESE MISMO hecho contable — el
/// tratamiento correcto es reversar el/los <c>JournalEntry</c> original(es)
/// (<c>JournalEntry.Reverse()</c>, vía <see cref="ReverseJournalEntryCommand"/>), nunca crear un
/// asiento compensatorio nuevo. A diferencia de Compras (un solo FactType, "InvoiceReceived"),
/// una venta autorizada puede haber generado DOS hechos contables independientes para el mismo
/// <c>SourceEventId</c> (InvoiceId): "InvoiceIssued" (ingreso/IVA/ICE + Caja/CxC, siempre que
/// exista PostingRule) y "CostOfGoodsSold" (costo de venta, solo si la venta tuvo líneas con
/// inventario y costo resuelto &gt; 0 — <see cref="SalesInvoiceCogsPostingTranslator"/>). Este
/// traductor reversa AMBOS, cada uno de forma independiente — la ausencia de uno de los dos (p.
/// ej. una venta sin inventario, sin CostOfGoodsSold) no bloquea la reversa del otro.
/// </summary>
/// <remarks>
/// Log-and-continue, mismo criterio que <see cref="PurchaseInvoiceCancelledPostingTranslator"/>: un
/// fallo al reversar (p. ej. período cerrado/bloqueado) nunca revierte la anulación de la venta ya
/// confirmada en Sales — la inconsistencia queda registrada en el log estructurado para revisión
/// manual, no bloquea al usuario (la anulación comercial, con reversa de Kardex, ya es irreversible
/// en este punto). Si un hecho nunca se contabilizó (p. ej. una factura autorizada ANTES de
/// SALES-JOURNAL-ENTRY-SILENT-FAILURE-01 Lote 2, sin asiento real por el bug ya corregido, o una
/// venta sin inventario que nunca generó CostOfGoodsSold), no hay nada que reversar para ese
/// FactType — se omite silenciosamente (info, no warning: no es un error). Idempotente por
/// construcción: <c>JournalEntry.Reverse()</c> solo opera sobre asientos <c>Posted</c> — un asiento
/// ya <c>Reversed</c> (p. ej. si este handler se reintentara) nunca se reversa dos veces, el
/// <c>FirstOrDefault(... Status == Posted)</c> de abajo ya no lo encuentra.
/// </remarks>
public sealed class SalesInvoiceCancelledPostingTranslator
    : INotificationHandler<SalesInvoiceCancelledEvent>
{
    private const string SourceModuleName = "Sales";
    private static readonly string[] FactTypesToReverse = ["InvoiceIssued", "CostOfGoodsSold"];

    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<SalesInvoiceCancelledPostingTranslator> _logger;

    public SalesInvoiceCancelledPostingTranslator(
        IJournalEntryRepository journalEntryRepository,
        IMediator mediator,
        ILogger<SalesInvoiceCancelledPostingTranslator> logger
    )
    {
        _journalEntryRepository = journalEntryRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(SalesInvoiceCancelledEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;

        // Una sola consulta trae todos los JournalEntry asociados a este InvoiceId (ambos
        // FactType, si existen) — mismo mecanismo que PurchaseInvoiceCancelledPostingTranslator,
        // ver IJournalEntryRepository.GetBySourceAsync (no filtra por SourceEventType porque un
        // mismo documento de origen puede tener más de un asiento asociado).
        var candidates = await _journalEntryRepository.GetBySourceAsync(
            tenantId,
            e.CompanyId,
            SourceModuleName,
            e.InvoiceId,
            ct
        );

        foreach (var factType in FactTypesToReverse)
        {
            var original = candidates.FirstOrDefault(x =>
                x.SourceEventType == factType && x.Status == JournalEntryStatus.Posted
            );

            if (original is null)
            {
                _logger.LogInformation(
                    "No hay asiento Posted que reversar para SalesInvoice {InvoiceId} ({InvoiceNumber}) "
                        + "FactType {FactType} — probablemente nunca se contabilizó (venta sin inventario, "
                        + "costo en cero, o sin PostingRule configurada en su momento).",
                    e.InvoiceId,
                    e.InvoiceNumber,
                    factType
                );
                continue;
            }

            var result = await _mediator.Send(
                new ReverseJournalEntryCommand(
                    original.Id,
                    $"Factura de venta {e.InvoiceNumber} anulada: {e.CancelReason}"
                ),
                ct
            );

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Reverso de asiento falló para SalesInvoice {InvoiceId} ({InvoiceNumber}) FactType "
                        + "{FactType}: {Code} — {Error}",
                    e.InvoiceId,
                    e.InvoiceNumber,
                    factType,
                    result.Code,
                    result.Error
                );
            }
        }
    }
}
