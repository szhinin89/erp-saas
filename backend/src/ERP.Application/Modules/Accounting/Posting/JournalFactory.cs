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
            SystemActor);

        foreach (var line in rule.Lines.OrderBy(l => l.SortOrder))
        {
            var amount = ResolveAmount(fact, line.AmountKind);
            if (amount == 0m)
                continue;

            var debit = line.Nature == AccountNature.Debit ? amount : 0m;
            var credit = line.Nature == AccountNature.Credit ? amount : 0m;
            entry.AddLine(line.AccountId, description, debit, credit);
        }

        return entry;
    }

    /// <summary>
    /// Único punto de mapeo entre <see cref="PostingAmountKind"/> y los montos ya resueltos por
    /// el módulo de origen en <see cref="PostingFact"/> — sin condicionales por SourceModule/
    /// FactType. <see cref="PostingAmountKind.Retention"/> resuelve a 0m: PostingFact todavía no
    /// transporta ese monto (fuera de alcance de esta fase, "No modificar PostingFact") — una
    /// PostingRuleLine que lo referencie queda sin efecto hasta que PostingFact se enriquezca.
    /// </summary>
    private static decimal ResolveAmount(PostingFact fact, PostingAmountKind kind) => kind switch
    {
        PostingAmountKind.Subtotal => fact.Subtotal,
        PostingAmountKind.TaxVat => fact.TotalVat,
        PostingAmountKind.TaxIce => fact.TotalIce,
        PostingAmountKind.Discount => fact.TotalDiscount,
        PostingAmountKind.GrandTotal => fact.GrandTotal,
        PostingAmountKind.Retention => 0m,
        _ => 0m,
    };
}
