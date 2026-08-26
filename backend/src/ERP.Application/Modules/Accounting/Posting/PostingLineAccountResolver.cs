using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — único punto de resolución de "qué cuenta usa
/// esta línea", compartido por <see cref="JournalFactory"/> (construcción del asiento) y
/// <see cref="PostingAccountGuard"/> (validación previa) para que ambos vean exactamente la misma
/// cuenta efectiva y nunca se valide una cuenta distinta de la que termina contabilizándose.
/// Puramente mecánico (AmountKind + Nature) — sin condicionales por SourceModule/FactType
/// (ADR-026 §6.2).
/// </summary>
internal static class PostingLineAccountResolver
{
    public static Guid ResolveAccountId(PostingFact fact, PostingRuleLine line) =>
        fact.OverrideAccountId is { } overrideAccountId
        && line.AmountKind == fact.OverrideAmountKind
        && line.Nature == fact.OverrideAccountNature
            ? overrideAccountId
            : line.AccountId;
}
