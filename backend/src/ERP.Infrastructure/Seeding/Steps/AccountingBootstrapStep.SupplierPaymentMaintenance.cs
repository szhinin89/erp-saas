using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C-PROD-CLOSE (ADR-035) — mantenimiento de las reglas
/// canónicas "Payables"/"SupplierPaymentConfirmed"/"SupplierPaymentReversed" en instalaciones
/// existentes, incluida Production. Mismo mecanismo exacto que
/// <c>SALES-INVOICE-POSTING-RULE-PROD-BACKFILL-02</c> (<see cref="MaintainSalesInvoiceRuleAsync"/>):
/// diagnóstico por contenido, y solo la forma canónica anterior exacta se actualiza.
/// </summary>
public sealed partial class AccountingBootstrapStep
{
    internal const string SupplierPaymentRuleCanonical = "Canonical";
    internal const string SupplierPaymentRuleLegacy = "Legacy";

    internal static readonly string[] SupplierPaymentFactTypes =
    [
        "SupplierPaymentConfirmed",
        "SupplierPaymentReversed",
    ];

    /// <summary>
    /// "Canonical" (forma vigente de <see cref="MinimalPostingRules"/>), "Legacy" (forma canónica
    /// anterior exacta: UNA línea CxP GrandTotal con la naturaleza de la regla, cabecera sin
    /// personalizar, todas las cuentas de la forma vigente disponibles) o "Custom: …" (cualquier
    /// otra cosa — nunca se sobrescribe). Una forma anterior con cuentas no disponibles se reporta
    /// como "Legacy; InvalidAccounts: …" y tampoco se toca (sin actualización parcial).
    /// </summary>
    private static string DiagnoseSupplierPaymentRule(
        PostingRule rule,
        Dictionary<string, AccountSeedLookup> accounts
    )
    {
        if (!rule.IsActive || rule.TaxCode is not null || rule.DebitAccountId is not null || rule.CreditAccountId is not null)
            return "Custom: inactive rule or customized header";

        var canonical = MinimalPostingRules
            .Single(r => r.SourceModule == "Payables" && r.FactType == rule.FactType)
            .Lines;
        var nature = canonical[0].Nature;

        bool Matches(IReadOnlyCollection<MinimalPostingRuleLine> expected)
        {
            if (rule.Lines.Count != expected.Count || expected.Any(l => !accounts.ContainsKey(l.AccountCode)))
                return false;
            var tuples = expected.Select(l => (accounts[l.AccountCode].Id, l.Nature, l.AmountKind)).ToHashSet();
            return tuples.Count == rule.Lines.Count
                && tuples.SetEquals(rule.Lines.Select(l => (l.AccountId, l.Nature, l.AmountKind)));
        }

        if (Matches(canonical))
            return SupplierPaymentRuleCanonical;
        if (!Matches([new MinimalPostingRuleLine("2.1.01.001", nature, PostingAmountKind.GrandTotal)]))
            return "Custom: unrecognized line combination";

        var invalid = canonical
            .Select(l => l.AccountCode)
            .Distinct()
            .Where(code => !accounts.TryGetValue(code, out var a) || !a.IsActive || !a.AllowsPosting)
            .ToArray();
        return invalid.Length == 0
            ? SupplierPaymentRuleLegacy
            : $"{SupplierPaymentRuleLegacy}; InvalidAccounts: {string.Join(", ", invalid)}";
    }

    // Bypass through the sanctioned PlatformQueryAccessor (no ambient tenant in a deployment
    // command); TenantId + CompanyId are re-applied explicitly in every query below.
    internal async Task<IReadOnlyList<(string FactType, string Diagnostic)>> MaintainSupplierPaymentRulesAsync(
        Guid tenantId,
        Guid companyId,
        bool apply,
        CancellationToken cancellationToken
    )
    {
        var rules = await _db.PostingRules.AsPlatformQuery().Include(r => r.Lines)
            .Where(r =>
                r.TenantId == tenantId
                && r.CompanyId == companyId
                && r.SourceModule == "Payables"
                && SupplierPaymentFactTypes.Contains(r.FactType)
            )
            .ToListAsync(cancellationToken);
        var accounts = await _db.Accounts.AsPlatformQuery()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .ToDictionaryAsync(
                a => a.Code.Value,
                a => new AccountSeedLookup(a.Id, a.IsActive, a.AllowsPosting),
                cancellationToken
            );

        var results = new List<(string, string)>();
        var changed = false;
        foreach (var factType in SupplierPaymentFactTypes)
        {
            var matching = rules.Where(r => r.FactType == factType).ToList();
            if (matching.Count != 1)
            {
                results.Add((factType, matching.Count == 0 ? "MissingRule: unchanged" : "Custom: multiple matching rules; unchanged"));
                continue;
            }

            var rule = matching[0];
            var diagnostic = DiagnoseSupplierPaymentRule(rule, accounts);
            if (apply && diagnostic == SupplierPaymentRuleLegacy)
            {
                var nature = MinimalPostingRules
                    .Single(r => r.SourceModule == "Payables" && r.FactType == factType)
                    .Lines[0].Nature;
                TryCorrectLegacySupplierPaymentRule(rule, nature, accounts, companyId);
                changed = true;
                results.Add((factType, $"{SupplierPaymentRuleLegacy} -> {SupplierPaymentRuleCanonical}: applied"));
                continue;
            }

            if (diagnostic != SupplierPaymentRuleCanonical && diagnostic != SupplierPaymentRuleLegacy)
                _logger.LogWarning(
                    "Payables/{FactType} company={CompanyId} rule={RuleId}: {Diagnostic} — requiere revisión manual; pagos con saldo no aplicado quedan bloqueados (fail-closed).",
                    factType,
                    companyId,
                    rule.Id,
                    diagnostic
                );
            results.Add((factType, diagnostic));
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);
        return results;
    }
}
