using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Domain.Modules.Expenses.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// EXPENSES-CANCEL-01 — anular un gasto ya contabilizado deshace ESE MISMO hecho contable: reversa
/// el <c>JournalEntry</c> original vía <see cref="ReverseJournalEntryCommand"/>, nunca un
/// <c>PostingFact</c> compensatorio nuevo ni un reverso construido manualmente.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-01 — comportamiento común de anulación (<see cref="AccountingPostingCancellation"/>):
/// obligación Pending/Failed sin asiento → Canceled; sin asiento ni obligación (Accounting no
/// aplicaba) → nada; asiento Posted → reverso. Un reverso fallido LANZA
/// <see cref="ExpensePostingFailedException"/>: la anulación completa hace rollback.
/// </remarks>
public sealed class ExpenseDocumentCancelledPostingTranslator
    : INotificationHandler<ExpenseDocumentCancelledEvent>
{
    private readonly AccountingPostingCancellation _cancellation;
    private readonly IMediator _mediator;

    public ExpenseDocumentCancelledPostingTranslator(
        AccountingPostingCancellation cancellation,
        IMediator mediator
    )
    {
        _cancellation = cancellation;
        _mediator = mediator;
    }

    public async Task Handle(ExpenseDocumentCancelledEvent e, CancellationToken ct)
    {
        var original = await _cancellation.PrepareAsync(
            e.TenantId!.Value,
            e.CompanyId,
            ExpenseDocumentConfirmedPostingTranslator.SourceModuleName,
            ExpenseDocumentConfirmedPostingTranslator.FactTypeName,
            e.ExpenseDocumentId,
            ct
        );
        if (original is null)
            return;

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
