using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Expenses.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce ExpenseDocumentConfirmedEvent (Expenses) a PostingFact e invoca IPostingEngine — no crea
/// JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase 3.4), mismo
/// criterio que el resto de traductores. Difiere de Purchases/Sales en una sola cosa, deliberada
/// (EXPENSES-CONFIRM-07, "No usar warning silencioso para Gastos"): si el posting falla, este
/// traductor LANZA en vez de solo registrar un warning — ver <see cref="ExpensePostingFailedException"/>
/// para por qué eso aborta correctamente la confirmación completa (rollback de la transacción
/// ambiente en <c>ErpDbContext.SaveChangesAsync</c>) en vez de dejar el documento Confirmed con un
/// asiento contable ausente.
/// </summary>
public sealed class ExpenseDocumentConfirmedPostingTranslator
    : INotificationHandler<ExpenseDocumentConfirmedEvent>
{
    private const string SourceModuleName = "Expenses";
    private const string FactTypeName = "DocumentConfirmed";

    private readonly IPostingEngine _postingEngine;

    public ExpenseDocumentConfirmedPostingTranslator(IPostingEngine postingEngine) =>
        _postingEngine = postingEngine;

    public async Task Handle(ExpenseDocumentConfirmedEvent e, CancellationToken ct)
    {
        // EXPENSES-POSTING-ALLOCATIONS-06 — una allocation Debe por línea de gasto, a su propia
        // cuenta contable snapshot (cardinalidad variable, no representable con PostingRule.Lines
        // fijas). El IVA acreditable (TotalVat) y el total por pagar (GrandTotal) sí usan
        // PostingRuleLine — cuentas fijas configuradas por la empresa (IVA compras, cuenta puente/
        // CxP), consistente con el resto del motor contable.
        var allocations = e
            .LineAllocations.Select(l => new PostingAllocation(
                l.AccountingAccountId,
                l.Amount,
                AccountNature.Debit,
                l.Description,
                l.ExpenseLineId
            ))
            .ToList();

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.ExpenseDocumentId,
            e.AccountingDate,
            Subtotal: allocations.Sum(a => a.Amount),
            TotalVat: e.TotalVat,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: e.GrandTotal,
            Allocations: allocations
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
            throw new ExpensePostingFailedException(
                result.Error ?? "No se pudo contabilizar el gasto.",
                result.Code
            );
    }
}
