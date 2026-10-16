using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Domain.Modules.Retentions.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-3 — anular una retención ya contabilizada deshace ESE MISMO
/// hecho contable: reversa el <c>JournalEntry</c> original vía <see cref="ReverseJournalEntryCommand"/>
/// — nunca resuelve cuentas nuevas, reutiliza las del asiento original.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-01 — comportamiento común de anulación (<see cref="AccountingPostingCancellation"/>):
/// obligación Pending/Failed sin asiento → Canceled; sin asiento ni obligación (Accounting no
/// aplicaba) → nada; asiento Posted → reverso. Un reverso fallido LANZA
/// <see cref="RetentionPostingFailedException"/>: la anulación completa (y la de su origen) hace
/// rollback.
/// </remarks>
public sealed class RetentionDocumentCancelledPostingTranslator
    : INotificationHandler<RetentionDocumentCancelledEvent>
{
    private readonly AccountingPostingCancellation _cancellation;
    private readonly IMediator _mediator;

    public RetentionDocumentCancelledPostingTranslator(
        AccountingPostingCancellation cancellation,
        IMediator mediator
    )
    {
        _cancellation = cancellation;
        _mediator = mediator;
    }

    public async Task Handle(RetentionDocumentCancelledEvent e, CancellationToken ct)
    {
        var original = await _cancellation.PrepareAsync(
            e.TenantId!.Value,
            e.CompanyId,
            RetentionDocumentIssuedPostingTranslator.SourceModuleName,
            RetentionDocumentIssuedPostingTranslator.FactTypeName,
            e.RetentionDocumentId,
            ct
        );
        if (original is null)
            return;

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
