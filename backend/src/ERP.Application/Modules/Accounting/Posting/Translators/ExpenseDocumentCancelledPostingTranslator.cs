using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// EXPENSES-CANCEL-01 — mismo criterio que <see cref="PurchaseInvoiceCancelledPostingTranslator"/>
/// para anular una factura de compra: anular un gasto ya contabilizado deshace ESE MISMO hecho
/// contable, así que reversa el <c>JournalEntry</c> original (<c>JournalEntry.Reverse()</c>, vía
/// <see cref="ReverseJournalEntryCommand"/>) en vez de publicar un <c>PostingFact</c> compensatorio
/// nuevo. Localiza el asiento original vía <see cref="IJournalEntryRepository.GetBySourceAsync"/> y
/// delega — nunca reversa manualmente (ADR-026 §8, Fase 3.4: los traductores no contienen lógica
/// financiera).
///
/// Difiere de <see cref="PurchaseInvoiceCancelledPostingTranslator"/> en la misma única cosa que ya
/// distingue a Expenses de Purchases/Sales (EXPENSES-CONFIRM-07, "No usar warning silencioso para
/// Gastos"): si no hay asiento Posted que reversar, o si el reverso falla, este traductor LANZA
/// <see cref="ExpensePostingFailedException"/> en vez de solo loguear — un gasto Confirmed siempre
/// tiene un asiento Posted real (<see cref="ExpenseDocumentConfirmedPostingTranslator"/> ya lanza si
/// el posting original falla), así que no encontrarlo al anular es una inconsistencia real, no un
/// caso normal a omitir.
/// </summary>
public sealed class ExpenseDocumentCancelledPostingTranslator
    : INotificationHandler<ExpenseDocumentCancelledEvent>
{
    private const string SourceModuleName = "Expenses";
    private const string DocumentConfirmedFactType = "DocumentConfirmed";

    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IMediator _mediator;

    public ExpenseDocumentCancelledPostingTranslator(
        IJournalEntryRepository journalEntryRepository,
        IMediator mediator
    )
    {
        _journalEntryRepository = journalEntryRepository;
        _mediator = mediator;
    }

    public async Task Handle(ExpenseDocumentCancelledEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;

        var candidates = await _journalEntryRepository.GetBySourceAsync(
            tenantId,
            e.CompanyId,
            SourceModuleName,
            e.ExpenseDocumentId,
            ct
        );

        var original = candidates.FirstOrDefault(x =>
            x.SourceEventType == DocumentConfirmedFactType && x.Status == JournalEntryStatus.Posted
        );

        if (original is null)
            throw new ExpensePostingFailedException(
                $"No se encontró el asiento contable del gasto {e.DocumentNumber} para reversar.",
                "JOURNAL_ENTRY_NOT_FOUND"
            );

        var result = await _mediator.Send(
            new ReverseJournalEntryCommand(
                original.Id,
                $"Gasto {e.DocumentNumber} anulado: {e.CancelReason}"
            ),
            ct
        );

        if (!result.IsSuccess)
            throw new ExpensePostingFailedException(
                result.Error ?? "No se pudo reversar la contabilización del gasto.",
                result.Code
            );
    }
}
