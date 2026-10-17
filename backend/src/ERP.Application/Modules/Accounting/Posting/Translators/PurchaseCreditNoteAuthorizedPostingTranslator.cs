using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="PurchaseCreditNoteAuthorizedEvent"/> al hecho
/// Purchases/PurchaseCreditNoteAuthorized y lo registra como obligación durable en la transacción
/// de la autorización (ACCOUNTING-CONTINUITY-04).
/// </summary>
/// <remarks>
/// Solo la NC tipo Discount llega a <c>PurchaseCreditNote.Authorize()</c>: la tipo Return se
/// contabiliza exclusivamente como <see cref="PurchaseReturnAuthorizedPostingTranslator"/>, sin
/// doble posting. Sin efecto de inventario ni excedente a SupplierCredit (Authorize no permite
/// superar el saldo). IVA/ICE/IRBPNR provienen de los importes ya congelados por la NC; una línea
/// IRBPNR faltante en la regla deja la obligación Failed, nunca bloquea la autorización.
/// </remarks>
public sealed class PurchaseCreditNoteAuthorizedPostingTranslator(
    AccountingPostingRecorder recorder,
    ICompanyClock companyClock
) : INotificationHandler<PurchaseCreditNoteAuthorizedEvent>
{
    public const string SourceModuleName = "Purchases";
    public const string FactTypeName = "PurchaseCreditNoteAuthorized";

    public async Task Handle(PurchaseCreditNoteAuthorizedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var entryDate = await companyClock.TodayAsync(e.CompanyId, tenantId, ct);
        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.PurchaseCreditNoteId,
                e.CreditNoteNumber,
                entryDate,
                entryDate,
                new AccountingPostingPayload(
                    e.Subtotal,
                    e.VatAmount,
                    e.IceAmount,
                    0m,
                    e.TotalAmount,
                    TotalIrbpnr: e.IrbpnrAmount,
                    AppliedToPayableAmount: e.AppliedToPayableAmount,
                    References: new Dictionary<string, Guid>
                    {
                        ["PurchaseInvoiceId"] = e.PurchaseInvoiceId,
                        ["SupplierId"] = e.SupplierId,
                    }
                )
            ),
            ct
        );
    }
}
