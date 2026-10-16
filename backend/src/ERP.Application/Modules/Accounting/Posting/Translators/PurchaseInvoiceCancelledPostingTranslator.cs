using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Application.Modules.Purchases.Exceptions;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-REVERSALS-05: anular una factura de compra ya contabilizada deshace ESE MISMO hecho
/// contable: reversa el <c>JournalEntry</c> original vía <see cref="ReverseJournalEntryCommand"/>
/// (mismas invariantes ya probadas: solo Posted, sin doble reverso, período abierto), nunca un
/// asiento compensatorio nuevo.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-01 — comportamiento común de anulación (<see cref="AccountingPostingCancellation"/>):
/// obligación Pending/Failed sin asiento → Canceled; sin asiento ni obligación (Accounting no
/// aplicaba) → nada; asiento Posted → reverso. Si el reverso falla, LANZA
/// <see cref="PurchasePostingFailedException"/> y la anulación completa hace rollback: antes solo
/// se registraba un warning y la compra quedaba anulada con su asiento vigente.
/// </remarks>
public sealed class PurchaseInvoiceCancelledPostingTranslator
    : INotificationHandler<PurchaseInvoiceCancelledEvent>
{
    private readonly AccountingPostingCancellation _cancellation;
    private readonly IMediator _mediator;
    private readonly ICurrentCompany _company;

    public PurchaseInvoiceCancelledPostingTranslator(
        AccountingPostingCancellation cancellation,
        IMediator mediator,
        ICurrentCompany company
    )
    {
        _cancellation = cancellation;
        _mediator = mediator;
        _company = company;
    }

    public async Task Handle(PurchaseInvoiceCancelledEvent e, CancellationToken ct)
    {
        var original = await _cancellation.PrepareAsync(
            e.TenantId!.Value,
            _company.CompanyId,
            PurchaseInvoiceConfirmedPostingTranslator.SourceModuleName,
            PurchaseInvoiceConfirmedPostingTranslator.FactTypeName,
            e.InvoiceId,
            ct
        );
        if (original is null)
            return;

        var result = await _mediator.Send(
            new ReverseJournalEntryCommand(
                original.Id,
                $"Factura de compra {e.InvoiceNumber} anulada: {e.CancelReason}"
            ),
            ct
        );

        if (!result.IsSuccess)
            throw new PurchasePostingFailedException(
                result.Error ?? "No se pudo reversar la contabilización de la compra.",
                result.Code
            );
    }
}
