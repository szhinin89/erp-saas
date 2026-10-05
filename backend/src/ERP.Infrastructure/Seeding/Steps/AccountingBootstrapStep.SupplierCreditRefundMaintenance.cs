using ERP.Domain.Modules.Accounting.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — mantenimiento de las reglas canónicas
/// "Purchases"/"SupplierCreditRefunded"/"SupplierCreditRefundReversed" en instalaciones existentes,
/// incluida Production (el backfill automático de <c>AccountingChartBackfillService.EnsureAsync</c>
/// no corre en Production). Mismo mecanismo que <see cref="MaintainSupplierPaymentRulesAsync"/>:
/// diagnóstico por contenido; con <c>apply</c> solo CREA la regla canónica cuando falta por
/// completo y sus cuentas están disponibles. Una regla existente (canónica o personalizada) nunca
/// se modifica. Las reglas obsoletas por destino (<c>"SupplierCreditRefunded:{código}"</c>, forma
/// previa a 02D-B que el traductor ya no consulta) se reportan y nunca se borran.
/// </summary>
public sealed partial class AccountingBootstrapStep
{
    internal const string SupplierCreditRefundRuleCanonical = "Canonical";
    internal const string SupplierCreditRefundRuleMissing = "MissingRule";

    internal static readonly string[] SupplierCreditRefundFactTypes =
    [
        "SupplierCreditRefunded",
        "SupplierCreditRefundReversed",
    ];

    private static MinimalPostingRule CanonicalSupplierCreditRefundRule(string factType) =>
        MinimalPostingRules.Single(r => r.SourceModule == "Purchases" && r.FactType == factType);

    private static string[] InvalidCanonicalAccounts(
        MinimalPostingRule canonical,
        Dictionary<string, AccountSeedLookup> accounts
    ) =>
        canonical
            .Lines.Select(l => l.AccountCode)
            .Distinct()
            .Where(code =>
                !accounts.TryGetValue(code, out var a) || !a.IsActive || !a.AllowsPosting
            )
            .ToArray();

    /// <summary>"Canonical" (forma vigente exacta) o "Custom: …" (cualquier otra — nunca se sobrescribe).</summary>
    private static string DiagnoseSupplierCreditRefundRule(
        PostingRule rule,
        Dictionary<string, AccountSeedLookup> accounts
    )
    {
        if (
            !rule.IsActive
            || rule.TaxCode is not null
            || rule.DebitAccountId is not null
            || rule.CreditAccountId is not null
        )
            return "Custom: inactive rule or customized header";

        var canonical = CanonicalSupplierCreditRefundRule(rule.FactType).Lines;
        if (
            rule.Lines.Count != canonical.Count
            || canonical.Any(l => !accounts.ContainsKey(l.AccountCode))
        )
            return "Custom: unrecognized line combination";

        var expected = canonical
            .Select(l => (accounts[l.AccountCode].Id, l.Nature, l.AmountKind))
            .ToHashSet();
        return expected.SetEquals(rule.Lines.Select(l => (l.AccountId, l.Nature, l.AmountKind)))
            ? SupplierCreditRefundRuleCanonical
            : "Custom: unrecognized line combination";
    }

    // Bypass through the sanctioned PlatformQueryAccessor (no ambient tenant in a deployment
    // command); TenantId + CompanyId are re-applied explicitly in every query below.
    internal async Task<
        IReadOnlyList<(string FactType, string Diagnostic)>
    > MaintainSupplierCreditRefundRulesAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        bool apply,
        CancellationToken cancellationToken
    )
    {
        var rules = await _db
            .PostingRules.AsPlatformQuery()
            .Include(r => r.Lines)
            .Where(r =>
                r.TenantId == tenantId
                && r.CompanyId == companyId
                && r.SourceModule == "Purchases"
                && (
                    r.FactType.StartsWith("SupplierCreditRefunded")
                    || r.FactType.StartsWith("SupplierCreditRefundReversed")
                )
            )
            .ToListAsync(cancellationToken);
        var accounts = await _db
            .Accounts.AsPlatformQuery()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .ToDictionaryAsync(
                a => a.Code.Value,
                a => new AccountSeedLookup(a.Id, a.IsActive, a.AllowsPosting),
                cancellationToken
            );

        var results = new List<(string, string)>();
        var changed = false;
        foreach (var factType in SupplierCreditRefundFactTypes)
        {
            var canonical = CanonicalSupplierCreditRefundRule(factType);
            var matching = rules.Where(r => r.FactType == factType).ToList();

            if (matching.Count > 1)
            {
                results.Add((factType, "Custom: multiple matching rules; unchanged"));
                continue;
            }

            if (matching.Count == 1)
            {
                var diagnostic = DiagnoseSupplierCreditRefundRule(matching[0], accounts);
                if (diagnostic != SupplierCreditRefundRuleCanonical)
                    _logger.LogWarning(
                        "Purchases/{FactType} company={CompanyId} rule={RuleId}: {Diagnostic} — regla personalizada preservada; revise que acredite/debite Anticipos a proveedores o los reembolsos quedarán bloqueados (fail-closed).",
                        factType,
                        companyId,
                        matching[0].Id,
                        diagnostic
                    );
                results.Add((factType, diagnostic));
                continue;
            }

            var invalid = InvalidCanonicalAccounts(canonical, accounts);
            if (invalid.Length > 0)
            {
                results.Add(
                    (
                        factType,
                        $"{SupplierCreditRefundRuleMissing}; InvalidAccounts: {string.Join(", ", invalid)}; unchanged"
                    )
                );
                continue;
            }

            if (!apply)
            {
                results.Add((factType, SupplierCreditRefundRuleMissing));
                continue;
            }

            var rule = PostingRule.Create(
                tenantId,
                companyId,
                "Purchases",
                factType,
                null,
                null,
                null,
                actorId
            );
            foreach (var line in canonical.Lines)
                rule.AddLine(accounts[line.AccountCode].Id, line.Nature, line.AmountKind);
            _db.PostingRules.Add(rule);
            changed = true;
            results.Add(
                (
                    factType,
                    $"{SupplierCreditRefundRuleMissing} -> {SupplierCreditRefundRuleCanonical}: created"
                )
            );
        }

        var obsoletePerDestination = rules.Count(r => r.FactType.Contains(':'));
        if (obsoletePerDestination > 0)
            results.Add(
                (
                    "SupplierCreditRefund*:{destino}",
                    $"Obsolete per-destination rules: {obsoletePerDestination} (no longer used; preserved, not deleted)"
                )
            );

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);
        return results;
    }
}
