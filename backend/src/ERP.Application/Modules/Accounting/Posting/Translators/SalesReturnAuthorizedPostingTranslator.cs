using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SalesReturnAuthorizedEvent al hecho Sales/SalesReturn y lo registra como obligación
/// durable en la transacción de la autorización (ACCOUNTING-CONTINUITY-04). No revierte el asiento
/// de la factura: es un hecho comercial nuevo cuyos efectos decide la PostingRule vigente.
/// SalesReturn no tiene fecha propia: se fija la fecha operativa de la Company al crearse.
/// </summary>
public sealed class SalesReturnAuthorizedPostingTranslator(
    AccountingPostingRecorder recorder,
    ICompanyClock companyClock
) : INotificationHandler<SalesReturnAuthorizedEvent>
{
    public const string SourceModuleName = "Sales";
    public const string FactTypeName = "SalesReturn";

    public async Task Handle(SalesReturnAuthorizedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var entryDate = await companyClock.TodayAsync(e.CompanyId, tenantId, ct);

        // ACCOUNTING-10 — destino del reembolso (SSOT RefundAllocations, ya resuelto por el
        // agregado): Caja → CashApplied, crédito CxC → PendingBalance. La línea de Caja de la regla
        // usa la cuenta de la caja de la sesión que registró el SaleRefund (override por identidad,
        // resuelto en cada intento), nunca una cuenta fija.
        var destinations = e.RefundCashRegisterId is { } cashRegisterId && e.CashRefunded > 0m
            ? new List<AccountingPostingDestination>
            {
                new(
                    AccountingPostingDestination.CashRegister,
                    cashRegisterId,
                    e.CashRefunded,
                    AccountNature.Credit,
                    OverridesAmountKind: PostingAmountKind.CashApplied
                ),
            }
            : null;

        // Mismo redondeo que SalesInvoiceAuthorizedPostingTranslator: el libro persiste
        // numeric(18,2) y ambos componentes conservan exacta su diferencia (Σ bases gravables).
        var subtotal = Math.Round(e.Subtotal, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
        var discount = Math.Round(e.TotalDiscount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);

        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.SalesReturnId,
                e.ReturnNumber,
                entryDate,
                entryDate,
                new AccountingPostingPayload(
                    subtotal,
                    e.TotalVat,
                    e.TotalIce,
                    discount,
                    e.GrandTotal,
                    TotalIrbpnr: e.TotalIrbpnr,
                    CashApplied: e.CashRefunded,
                    PendingBalance: e.ReceivableCredited,
                    Destinations: destinations,
                    References: new Dictionary<string, Guid> { ["SalesInvoiceId"] = e.SalesInvoiceId }
                )
            ),
            ct
        );
    }
}
