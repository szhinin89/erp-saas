using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Construye el JournalEntry y sus JournalEntryLine vía JournalEntry.Create()/AddLine() — sin
/// DTO intermedio (ADR-026 §8, Fase 3.5.5). Cada línea de <see cref="PostingRule.Lines"/> declara
/// una cuenta, una naturaleza (Debit/Credit) y un <see cref="PostingAmountKind"/> — el monto se
/// resuelve exclusivamente por ese dato (ver <see cref="ResolveAmount"/>), nunca por
/// <c>SourceModule</c>/<c>FactType</c>: ningún condicional cerrado por tipo de hecho contable
/// (ADR-026 §6.2). Líneas cuyo monto resuelto es cero se omiten (p. ej. ICE ausente en la
/// factura) — nunca se contabiliza una línea en cero.
/// </summary>
internal sealed class JournalFactory
{
    private static readonly Guid SystemActor = Guid.Empty;

    public JournalEntry Create(PostingFact fact, PostingRule rule, AccountingPeriod period)
    {
        var description = $"{fact.SourceModule} — {fact.FactType} — {fact.SourceEventId}";

        var entry = JournalEntry.Create(
            fact.TenantId,
            fact.CompanyId,
            fact.EntryDate,
            period.Id,
            period.FiscalYear,
            fact.SourceModule,
            fact.FactType,
            fact.SourceEventId,
            description,
            SystemActor
        );

        foreach (var line in rule.Lines.OrderBy(l => l.SortOrder))
        {
            var amount = ResolveAmount(fact, line.AmountKind);
            if (amount == 0m)
                continue;

            var debit = line.Nature == AccountNature.Debit ? amount : 0m;
            var credit = line.Nature == AccountNature.Credit ? amount : 0m;
            var accountId = PostingLineAccountResolver.ResolveAccountId(fact, line);
            entry.AddLine(accountId, description, debit, credit);
        }

        // EXPENSES-POSTING-ALLOCATIONS-06 — líneas dinámicas por cuenta, además (nunca en lugar)
        // de las líneas fijas de PostingRule.Lines arriba. Cardinalidad variable, resuelta por el
        // módulo de origen (cada PostingAllocation ya validó su propia cuenta y monto > 0 en su
        // constructor — ver PostingAllocation.cs), nunca por SourceModule/FactType (ADR-026 §6.2).
        if (fact.Allocations is { Count: > 0 } allocations)
        {
            foreach (var allocation in allocations)
            {
                var debit = allocation.Nature == AccountNature.Debit ? allocation.Amount : 0m;
                var credit = allocation.Nature == AccountNature.Credit ? allocation.Amount : 0m;
                entry.AddLine(
                    allocation.AccountingAccountId,
                    allocation.Description ?? description,
                    debit,
                    credit
                );
            }
        }

        return entry;
    }

    /// <summary>
    /// Único punto de mapeo entre <see cref="PostingAmountKind"/> y los montos ya resueltos por
    /// el módulo de origen en <see cref="PostingFact"/> — sin condicionales por SourceModule/
    /// FactType. <see cref="PostingAmountKind.Retention"/> resuelve
    /// <see cref="PostingFact.RetainedAmount"/> desde <c>RETENTIONS-EXPENSES-INTEGRATION-01D-2</c>
    /// (antes resolvía siempre 0m porque el campo no existía — ver historial en git blame de este
    /// comentario) — el gap quedaba explícitamente reservado para cuando PostingFact se enriqueciera.
    ///
    /// P0-02 Fase 6 (Remediación 01) — 5 casos nuevos, cada uno resuelve el campo nullable nuevo
    /// correspondiente de <see cref="PostingFact"/> (§19.1bis, PurchaseReturnAuthorized); ninguno
    /// de los 6 casos preexistentes cambió de comportamiento.
    /// </summary>
    private static decimal ResolveAmount(PostingFact fact, PostingAmountKind kind) =>
        kind switch
        {
            PostingAmountKind.Subtotal => fact.Subtotal,
            PostingAmountKind.TaxVat => fact.TotalVat,
            PostingAmountKind.TaxIce => fact.TotalIce,
            PostingAmountKind.Discount => fact.TotalDiscount,
            PostingAmountKind.GrandTotal => fact.GrandTotal,
            PostingAmountKind.Retention => fact.RetainedAmount ?? 0m,
            PostingAmountKind.AppliedToPayable => fact.AppliedToPayableAmount ?? 0m,
            PostingAmountKind.SupplierCredit => fact.SupplierCreditAmount ?? 0m,
            PostingAmountKind.CostVarianceDebit => fact.CostVarianceDebitAmount ?? 0m,
            PostingAmountKind.CostVarianceCredit => fact.CostVarianceCreditAmount ?? 0m,
            PostingAmountKind.HistoricalCost => fact.HistoricalCostTotal ?? 0m,
            // FLOW-READY-02F.2 — IRBPNR (Compras); mismo criterio: campo nullable nuevo de PostingFact.
            PostingAmountKind.TaxIrbpnr => fact.TotalIrbpnr ?? 0m,
            _ => 0m,
        };
}
