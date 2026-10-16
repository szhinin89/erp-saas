using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce PurchaseInvoiceConfirmedEvent (Purchases) al hecho contable InvoiceReceived y lo registra
/// como obligación durable en la misma transacción que Compra/Kardex/CxP — no invoca el Posting
/// Engine, no resuelve cuentas, no contiene lógica financiera.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-01 — un fallo contable ya no revierte la compra: el intento ocurre tras el
/// commit (<see cref="AccountingPostingExecutor"/>) y queda Posted o Failed durable. Subtotal =
/// <c>CostSubtotal</c>: base neta de descuentos + flete/otros costos ya congelados por Compras, para
/// que Debe (Subtotal+impuestos) cuadre con Haber (GrandTotal) sin una segunda fórmula de costos.
/// </remarks>
public sealed class PurchaseInvoiceConfirmedPostingTranslator
    : INotificationHandler<PurchaseInvoiceConfirmedEvent>
{
    public const string SourceModuleName = "Purchases";
    public const string FactTypeName = "InvoiceReceived";

    private readonly AccountingPostingRecorder _recorder;

    public PurchaseInvoiceConfirmedPostingTranslator(AccountingPostingRecorder recorder) =>
        _recorder = recorder;

    public Task Handle(PurchaseInvoiceConfirmedEvent e, CancellationToken ct) =>
        _recorder.RecordAsync(
            new AccountingPostingRequest(
                e.TenantId!.Value,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.InvoiceId,
                e.InvoiceNumber,
                e.IssueDate,
                e.IssueDate,
                new AccountingPostingPayload(
                    e.CostSubtotal,
                    e.TotalVat,
                    e.TotalIce,
                    e.TotalDiscount,
                    e.GrandTotal,
                    TotalIrbpnr: e.TotalIrbpnr
                )
            ),
            ct
        );
}
