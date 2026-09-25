using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Seeding.Steps;

public sealed partial class AccountingBootstrapStep
{
    private static string DiagnoseSalesInvoiceRule(
        PostingRule rule, Dictionary<string, AccountSeedLookup> accounts)
    {
        if (!rule.IsActive || rule.TaxCode is not null || rule.DebitAccountId is not null || rule.CreditAccountId is not null)
            return "Custom: inactive rule or customized header";

        var canonical = MinimalPostingRules.Single(r => r.SourceModule == "Sales" && r.FactType == "InvoiceIssued").Lines;
        bool Matches(IEnumerable<MinimalPostingRuleLine> expected)
        {
            var lines = expected.ToArray();
            if (rule.Lines.Count != lines.Length || lines.Any(l => !accounts.ContainsKey(l.AccountCode)))
                return false;
            var tuples = lines.Select(l => (accounts[l.AccountCode].Id, l.Nature, l.AmountKind)).ToHashSet();
            return tuples.Count == rule.Lines.Count && tuples.SetEquals(rule.Lines.Select(l => (l.AccountId, l.Nature, l.AmountKind)));
        }

        var five = canonical.Where(l => l.AmountKind != PostingAmountKind.Discount && l.AmountKind != PostingAmountKind.TaxIrbpnr).ToArray();
        var four = five.Where(l => l.Nature == AccountNature.Credit).Append(LegacySalesInvoiceIssuedDebitLine);
        var shape = Matches(canonical) ? "Canonical7" : Matches(four) ? "Legacy4" : Matches(five) ? "Legacy5" : "Custom: unrecognized line combination";
        var invalid = canonical.Select(l => l.AccountCode).Distinct().Where(code =>
            !accounts.TryGetValue(code, out var account) || !account.IsActive || !account.AllowsPosting).ToArray();
        return invalid.Length == 0 ? shape : $"{shape}; InvalidAccounts: {string.Join(", ", invalid)}";
    }

    // Bypass through the sanctioned PlatformQueryAccessor (no ambient tenant in a deployment
    // command); TenantId + CompanyId are re-applied explicitly in every query below.
    internal async Task<string> MaintainSalesInvoiceRuleAsync(Guid tenantId, Guid companyId, bool apply, CancellationToken cancellationToken)
    {
        var rules = await _db.PostingRules.AsPlatformQuery().Include(r => r.Lines)
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId && r.SourceModule == "Sales" && r.FactType == "InvoiceIssued")
            .ToListAsync(cancellationToken);
        if (rules.Count != 1)
            return rules.Count == 0 ? "MissingRule: unchanged" : "Custom: multiple matching rules; unchanged";

        var accounts = await _db.Accounts.AsPlatformQuery()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .ToDictionaryAsync(a => a.Code.Value, a => new AccountSeedLookup(a.Id, a.IsActive, a.AllowsPosting), cancellationToken);
        var status = DiagnoseSalesInvoiceRule(rules[0], accounts);
        if (apply && (status == "Legacy4" || status == "Legacy5"))
        {
            TryCorrectLegacySalesInvoiceIssuedRule(rules[0], accounts, companyId);
            await _db.SaveChangesAsync(cancellationToken);
            return $"{status} -> Canonical7: applied";
        }
        return status;
    }
}
