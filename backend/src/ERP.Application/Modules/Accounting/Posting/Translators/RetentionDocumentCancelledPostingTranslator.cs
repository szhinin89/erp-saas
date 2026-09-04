using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Retentions.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-3 — mismo criterio que
/// <see cref="ExpenseDocumentCancelledPostingTranslator"/>: anular una retención ya contabilizada
/// deshace ESE MISMO hecho contable, así que reversa el <c>JournalEntry</c> original
/// (<c>JournalEntry.Reverse()</c>, vía <see cref="ReverseJournalEntryCommand"/>) en vez de publicar
/// un <c>PostingFact</c> compensatorio nuevo — nunca resuelve cuentas nuevas, reutiliza las ya
/// resueltas en el asiento original de <see cref="RetentionDocumentIssuedPostingTranslator"/>.
/// Localiza el asiento original vía <see cref="IJournalEntryRepository.GetBySourceAsync"/> con
/// <c>SourceModule="Retentions"</c>+<c>SourceEventId=RetentionDocumentId</c>, filtrando por
/// <c>SourceEventType="DocumentIssued"</c>+<c>Status=Posted</c> — delega el reverso, nunca lo
/// construye manualmente (ADR-026 §8, Fase 3.4: los traductores no contienen lógica financiera).
///
/// Posting ESTRICTO (mismo criterio que el resto de Retentions/Expenses, EXPENSES-CONFIRM-07): si
/// no hay asiento Posted que reversar, o si el reverso falla, LANZA
/// <see cref="RetentionPostingFailedException"/> en vez de solo loguear — una retención Issued
/// siempre tiene un asiento Posted real (<see cref="RetentionDocumentIssuedPostingTranslator"/> ya
/// lanza si el posting original falla), así que no encontrarlo al anular es una inconsistencia
/// real, no un caso normal a omitir.
/// </summary>
public sealed class RetentionDocumentCancelledPostingTranslator
    : INotificationHandler<RetentionDocumentCancelledEvent>
{
    private const string SourceModuleName = "Retentions";
    private const string DocumentIssuedFactType = "DocumentIssued";

    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IMediator _mediator;

    public RetentionDocumentCancelledPostingTranslator(
        IJournalEntryRepository journalEntryRepository,
        IMediator mediator
    )
    {
        _journalEntryRepository = journalEntryRepository;
        _mediator = mediator;
    }

    public async Task Handle(RetentionDocumentCancelledEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;

        var candidates = await _journalEntryRepository.GetBySourceAsync(
            tenantId,
            e.CompanyId,
            SourceModuleName,
            e.RetentionDocumentId,
            ct
        );

        var original = candidates.FirstOrDefault(x =>
            x.SourceEventType == DocumentIssuedFactType && x.Status == JournalEntryStatus.Posted
        );

        if (original is null)
            throw new RetentionPostingFailedException(
                $"No se encontró el asiento contable de la retención {e.RetentionNumber} para reversar.",
                "JOURNAL_ENTRY_NOT_FOUND"
            );

        var result = await _mediator.Send(
            new ReverseJournalEntryCommand(
                original.Id,
                $"Retención {e.RetentionNumber} anulada: {e.CancelReason}"
            ),
            ct
        );

        if (!result.IsSuccess)
            throw new RetentionPostingFailedException(
                result.Error ?? "No se pudo reversar la contabilización de la retención.",
                result.Code
            );
    }
}
