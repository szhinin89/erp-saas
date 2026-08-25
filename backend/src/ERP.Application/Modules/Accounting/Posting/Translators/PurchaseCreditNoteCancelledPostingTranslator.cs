using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-CREDIT-NOTES-POSTING-08: mismo criterio arquitectónico que
/// <see cref="PurchaseInvoiceCancelledPostingTranslator"/> (ACCOUNTING-REVERSALS-05) — cancelar una
/// nota de crédito ya contabilizada deshace ESE MISMO hecho contable (no un documento nuevo, a
/// diferencia de SalesReturn/PurchaseReturn) — el tratamiento correcto es reversar el
/// <c>JournalEntry</c> original vía <see cref="ReverseJournalEntryCommand"/>, nunca un asiento
/// compensatorio nuevo. Reutiliza <see cref="ReverseJournalEntryCommandHandler"/> tal cual (mismas
/// invariantes ya probadas: solo Posted, sin doble reverso, período abierto).
/// </summary>
/// <remarks>
/// <see cref="PurchaseCreditNoteCancelledEvent.AppliedToPayableAmount"/> es <c>null</c> cuando la
/// NC se cancela desde <c>Draft</c> — nunca llegó a autorizarse, nunca generó asiento, nada que
/// reversar (no es un error, se omite silenciosamente). Log-and-continue: un fallo al reversar
/// nunca revierte la cancelación de la NC ya confirmada en Purchases.
/// </remarks>
public sealed class PurchaseCreditNoteCancelledPostingTranslator
    : INotificationHandler<PurchaseCreditNoteCancelledEvent>
{
    private const string SourceModuleName = "Purchases";
    private const string CreditNoteAuthorizedFactType = "PurchaseCreditNoteAuthorized";

    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<PurchaseCreditNoteCancelledPostingTranslator> _logger;

    public PurchaseCreditNoteCancelledPostingTranslator(
        IJournalEntryRepository journalEntryRepository,
        IMediator mediator,
        ILogger<PurchaseCreditNoteCancelledPostingTranslator> logger
    )
    {
        _journalEntryRepository = journalEntryRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(PurchaseCreditNoteCancelledEvent e, CancellationToken ct)
    {
        if (e.AppliedToPayableAmount is null)
        {
            _logger.LogInformation(
                "PurchaseCreditNote {PurchaseCreditNoteId} ({CreditNoteNumber}) se canceló desde Draft — "
                    + "nunca se contabilizó, nada que reversar.",
                e.PurchaseCreditNoteId,
                e.CreditNoteNumber
            );
            return;
        }

        var tenantId = e.TenantId!.Value;

        var candidates = await _journalEntryRepository.GetBySourceAsync(
            tenantId,
            e.CompanyId,
            SourceModuleName,
            e.PurchaseCreditNoteId,
            ct
        );

        var original = candidates.FirstOrDefault(x =>
            x.SourceEventType == CreditNoteAuthorizedFactType && x.Status == JournalEntryStatus.Posted
        );

        if (original is null)
        {
            _logger.LogInformation(
                "No hay asiento Posted que reversar para PurchaseCreditNote {PurchaseCreditNoteId} ({CreditNoteNumber}).",
                e.PurchaseCreditNoteId,
                e.CreditNoteNumber
            );
            return;
        }

        var result = await _mediator.Send(
            new ReverseJournalEntryCommand(
                original.Id,
                $"Nota de crédito de compra {e.CreditNoteNumber} anulada: {e.Reason}"
            ),
            ct
        );

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Reverso de asiento falló para PurchaseCreditNote {PurchaseCreditNoteId} ({CreditNoteNumber}): {Code} — {Error}",
                e.PurchaseCreditNoteId,
                e.CreditNoteNumber,
                result.Code,
                result.Error
            );
        }
    }
}
