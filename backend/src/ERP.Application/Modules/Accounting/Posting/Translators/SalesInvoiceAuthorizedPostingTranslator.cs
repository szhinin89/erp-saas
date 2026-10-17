using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SalesInvoiceAuthorizedEvent (Sales) al hecho Sales/InvoiceIssued y lo registra como
/// obligación durable en la misma transacción de la autorización — no invoca el Posting Engine ni
/// resuelve cuentas (ACCOUNTING-CONTINUITY-04, accounting.md §4).
/// </summary>
/// <remarks>
/// Persiste los importes del documento, el contado real (<c>CashApplied</c>) separado del saldo
/// CxC (<c>PendingBalance</c>) y el destino de cada cobro por identidad (caja de la sesión, cuenta
/// bancaria, medio tarjeta/cheque). Cada intento resuelve la cuenta vigente de esos destinos como
/// allocations Debe; el <c>CashApplied</c> sin destino (solo callers sin desglose) queda contra la
/// línea fija de la regla. Un fallo contable deja la obligación Failed sin revertir la venta.
/// </remarks>
public sealed class SalesInvoiceAuthorizedPostingTranslator(AccountingPostingRecorder recorder)
    : INotificationHandler<SalesInvoiceAuthorizedEvent>
{
    public const string SourceModuleName = "Sales";
    public const string FactTypeName = "InvoiceIssued";

    public Task Handle(SalesInvoiceAuthorizedEvent e, CancellationToken ct)
    {
        // SalesSettlementPolicy: saldo CxC = GrandTotal - contado, nunca negativo.
        var pendingBalance = Math.Max(e.GrandTotal - e.CashApplied, 0m);
        var destinations = e
            .CollectionDestinations.Where(d => d.Amount > 0m)
            .Select(d => new AccountingPostingDestination(KindOf(d.Kind), d.ReferenceId, d.Amount, AccountNature.Debit))
            .ToList();
        var ruleLineCash = Math.Max(e.CashApplied - destinations.Sum(d => d.Amount), 0m);

        // SALES-DISCOUNT-PERSISTENCE-REVERSAL-01: el libro persiste numeric(18,2). Ambos
        // componentes se redondean igual para conservar exacta su diferencia (Σ bases gravables).
        var subtotal = Math.Round(e.Subtotal, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
        var discount = Math.Round(e.TotalDiscount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);

        return recorder.RecordAsync(
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
                    subtotal,
                    e.TotalVat,
                    e.TotalIce,
                    discount,
                    e.GrandTotal,
                    TotalIrbpnr: e.TotalIrbpnr,
                    CashApplied: ruleLineCash,
                    PendingBalance: pendingBalance,
                    Destinations: destinations,
                    References: new Dictionary<string, Guid> { ["CashSessionId"] = e.CashSessionId }
                )
            ),
            ct
        );
    }

    private static string KindOf(SalesCollectionDestinationKind kind) =>
        kind switch
        {
            SalesCollectionDestinationKind.CashRegister => AccountingPostingDestination.CashRegister,
            SalesCollectionDestinationKind.BankAccount => AccountingPostingDestination.BankAccount,
            SalesCollectionDestinationKind.PaymentMethod => AccountingPostingDestination.PaymentMethod,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Destino de cobro no soportado."),
        };
}
