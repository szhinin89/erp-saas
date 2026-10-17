using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce CollectionAppliedEvent (Finance) al hecho Finance/CollectionApplied y lo registra como
/// obligación durable en la transacción del cobro (ACCOUNTING-CONTINUITY-04). Un cobro no tiene
/// desglose de impuestos: GrandTotal transporta el monto cobrado.
/// </summary>
/// <remarks>
/// Con caja o cuenta bancaria elegida, su identidad sustituye en cada intento la cuenta de la
/// línea Debe GrandTotal de la regla por la cuenta vigente de ese destino; sin destino se usa la
/// cuenta de la regla. Destino sin cuenta o regla inválida dejan la obligación Failed.
/// </remarks>
public sealed class CollectionAppliedPostingTranslator(AccountingPostingRecorder recorder)
    : INotificationHandler<CollectionAppliedEvent>
{
    public const string SourceModuleName = "Finance";
    public const string FactTypeName = "CollectionApplied";

    public Task Handle(CollectionAppliedEvent e, CancellationToken ct)
    {
        IReadOnlyList<AccountingPostingDestination>? destinations =
            e.CompanyBankAccountId is null && e.CashRegisterId is null
                ? null
                :
                [
                    AccountingPostingDestination.FromCashOrBank(
                        e.CompanyBankAccountId,
                        e.CashRegisterId,
                        e.Amount,
                        AccountNature.Debit,
                        PostingAmountKind.GrandTotal
                    ),
                ];

        return recorder.RecordAsync(
            new AccountingPostingRequest(
                e.TenantId!.Value,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.PaymentId,
                null,
                e.PaymentDate,
                e.PaymentDate,
                new AccountingPostingPayload(
                    0m,
                    0m,
                    0m,
                    0m,
                    e.Amount,
                    Destinations: destinations,
                    References: new Dictionary<string, Guid> { ["CustomerId"] = e.CustomerId }
                )
            ),
            ct
        );
    }
}
