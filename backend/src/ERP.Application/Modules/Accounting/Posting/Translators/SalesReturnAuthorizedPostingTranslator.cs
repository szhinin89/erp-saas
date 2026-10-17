using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
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
                    e.Subtotal,
                    e.TotalVat,
                    e.TotalIce,
                    e.TotalDiscount,
                    e.GrandTotal,
                    TotalIrbpnr: e.TotalIrbpnr,
                    References: new Dictionary<string, Guid> { ["SalesInvoiceId"] = e.SalesInvoiceId }
                )
            ),
            ct
        );
    }
}
