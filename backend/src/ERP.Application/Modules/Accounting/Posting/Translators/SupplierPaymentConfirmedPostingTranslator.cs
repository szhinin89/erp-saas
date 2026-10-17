using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Payables.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <c>SupplierPaymentConfirmedEvent</c> (Payables) al hecho Payables/SupplierPaymentConfirmed
/// y lo registra como obligación durable en la transacción del pago (ACCOUNTING-CONTINUITY-04).
/// </summary>
/// <remarks>
/// Asiento: Debe CxP por lo aplicado (<c>AppliedToPayable</c>) + Debe anticipos por el remanente
/// (<c>SupplierCredit</c>, ADR-035) y un Haber por cada medio contra la cuenta vigente de su caja o
/// banco (allocation por identidad). El <c>SupplierCredit</c> creado por el remanente no genera otro
/// asiento: este es su único reconocimiento. La línea de anticipos faltante en la regla, la cuenta
/// de caja/banco faltante o inválida dejan la obligación Failed, nunca rechazan el pago.
/// </remarks>
public sealed class SupplierPaymentConfirmedPostingTranslator(AccountingPostingRecorder recorder)
    : INotificationHandler<SupplierPaymentConfirmedEvent>
{
    public const string SourceModuleName = "Payables";
    public const string FactTypeName = "SupplierPaymentConfirmed";

    public Task Handle(SupplierPaymentConfirmedEvent e, CancellationToken ct) =>
        recorder.RecordAsync(
            new AccountingPostingRequest(
                e.TenantId!.Value,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.SupplierPaymentId,
                null,
                e.PaymentDate,
                e.PaymentDate,
                new AccountingPostingPayload(
                    0m,
                    0m,
                    0m,
                    0m,
                    e.TotalAmount,
                    AppliedToPayableAmount: e.AppliedAmount,
                    SupplierCreditAmount: e.UnappliedAmount,
                    Destinations: e
                        .MethodLines.Select(l =>
                            AccountingPostingDestination.FromCashOrBank(
                                l.CompanyBankAccountId,
                                l.CashRegisterId,
                                l.Amount,
                                AccountNature.Credit
                            )
                        )
                        .ToList(),
                    References: new Dictionary<string, Guid> { ["SupplierId"] = e.SupplierId }
                )
            ),
            ct
        );
}
