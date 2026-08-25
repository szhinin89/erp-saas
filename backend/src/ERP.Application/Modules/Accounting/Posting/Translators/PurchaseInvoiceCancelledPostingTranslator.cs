using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-REVERSALS-05: a diferencia de <see cref="PurchaseReturnAuthorizedPostingTranslator"/>/
/// <see cref="PurchaseReturnCancelledPostingTranslator"/> (que publican un <c>PostingFact</c>
/// NUEVO e independiente — una devolución es un documento distinto de la factura original),
/// anular una factura de compra ya contabilizada deshace ESE MISMO hecho contable: el tratamiento
/// correcto es reversar el <c>JournalEntry</c> original (<c>JournalEntry.Reverse()</c>, vía
/// <see cref="ReverseJournalEntryCommand"/>), no crear un asiento compensatorio nuevo. Reutiliza
/// <see cref="ReverseJournalEntryCommandHandler"/> tal cual (mismas invariantes ya probadas:
/// solo Posted, sin doble reverso, período abierto) — este traductor solo localiza el
/// JournalEntry original vía <see cref="IJournalEntryRepository.GetBySourceAsync"/> y delega.
/// </summary>
/// <remarks>
/// Log-and-continue, mismo criterio que el resto de translators (ver
/// <c>SalesInvoiceAuthorizedPostingTranslator</c>): un fallo al reversar (p. ej. período
/// cerrado/bloqueado) nunca revierte la anulación de la compra ya confirmada en Purchases — la
/// inconsistencia queda registrada en el log estructurado para revisión manual, no bloquea al
/// usuario. Si la factura nunca se contabilizó (sin <c>PostingRule</c> configurada en su momento,
/// o sin asiento <c>Posted</c> localizable), no hay nada que reversar — se omite silenciosamente
/// (info, no warning: no es un error).
/// </remarks>
public sealed class PurchaseInvoiceCancelledPostingTranslator
    : INotificationHandler<PurchaseInvoiceCancelledEvent>
{
    private const string SourceModuleName = "Purchases";
    private const string InvoiceReceivedFactType = "InvoiceReceived";

    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IMediator _mediator;
    private readonly ICurrentCompany _company;
    private readonly ILogger<PurchaseInvoiceCancelledPostingTranslator> _logger;

    public PurchaseInvoiceCancelledPostingTranslator(
        IJournalEntryRepository journalEntryRepository,
        IMediator mediator,
        ICurrentCompany company,
        ILogger<PurchaseInvoiceCancelledPostingTranslator> logger
    )
    {
        _journalEntryRepository = journalEntryRepository;
        _mediator = mediator;
        _company = company;
        _logger = logger;
    }

    public async Task Handle(PurchaseInvoiceCancelledEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var companyId = _company.CompanyId;

        var candidates = await _journalEntryRepository.GetBySourceAsync(
            tenantId,
            companyId,
            SourceModuleName,
            e.InvoiceId,
            ct
        );

        var original = candidates.FirstOrDefault(x =>
            x.SourceEventType == InvoiceReceivedFactType && x.Status == JournalEntryStatus.Posted
        );

        if (original is null)
        {
            _logger.LogInformation(
                "No hay asiento Posted que reversar para PurchaseInvoice {InvoiceId} ({InvoiceNumber}) — "
                    + "probablemente nunca se contabilizó (sin PostingRule configurada en su momento).",
                e.InvoiceId,
                e.InvoiceNumber
            );
            return;
        }

        var result = await _mediator.Send(
            new ReverseJournalEntryCommand(
                original.Id,
                $"Factura de compra {e.InvoiceNumber} anulada: {e.CancelReason}"
            ),
            ct
        );

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Reverso de asiento falló para PurchaseInvoice {InvoiceId} ({InvoiceNumber}): {Code} — {Error}",
                e.InvoiceId,
                e.InvoiceNumber,
                result.Code,
                result.Error
            );
        }
    }
}
