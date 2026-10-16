using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Expenses.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce ExpenseDocumentConfirmedEvent (Expenses) al hecho contable DocumentConfirmed y lo
/// registra como obligación durable en la misma transacción que el gasto y su CxP — no invoca el
/// Posting Engine, no resuelve cuentas.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-01 — la cuenta de cada línea ya no es dato del gasto: el payload conserva
/// la subcategoría e importe de cada línea, y la cuenta se resuelve en cada intento con la
/// configuración vigente (<see cref="AccountingPostingFactBuilder"/>). Una cuenta faltante deja la
/// obligación Failed; nunca revierte el gasto.
/// </remarks>
public sealed class ExpenseDocumentConfirmedPostingTranslator
    : INotificationHandler<ExpenseDocumentConfirmedEvent>
{
    public const string SourceModuleName = "Expenses";
    public const string FactTypeName = "DocumentConfirmed";

    private readonly AccountingPostingRecorder _recorder;

    public ExpenseDocumentConfirmedPostingTranslator(AccountingPostingRecorder recorder) =>
        _recorder = recorder;

    public Task Handle(ExpenseDocumentConfirmedEvent e, CancellationToken ct)
    {
        var lines = e
            .LineAllocations.Select(l => new AccountingPostingExpenseLine(
                l.ExpenseLineId,
                l.ExpenseSubcategoryId,
                l.Amount,
                l.Description
            ))
            .ToList();

        return _recorder.RecordAsync(
            new AccountingPostingRequest(
                e.TenantId!.Value,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.ExpenseDocumentId,
                e.DocumentNumber,
                e.AccountingDate,
                e.AccountingDate,
                new AccountingPostingPayload(
                    Subtotal: lines.Sum(l => l.Amount),
                    TotalVat: e.TotalVat,
                    TotalIce: 0m,
                    TotalDiscount: 0m,
                    GrandTotal: e.GrandTotal,
                    ExpenseLines: lines
                )
            ),
            ct
        );
    }
}
