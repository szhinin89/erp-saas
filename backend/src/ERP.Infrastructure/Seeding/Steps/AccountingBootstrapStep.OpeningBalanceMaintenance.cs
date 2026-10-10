using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// IL-7A — configuración contable de la apertura de la Carga Inicial: cuenta puente patrimonial
/// "3.1.04.001 Saldos de apertura" (bajo el agrupador "3.1.04") y las reglas
/// <c>InitialLoad/OpeningInventory|OpeningReceivables|OpeningPayables</c>. Una empresa nueva las
/// recibe por <see cref="ExecuteAsync"/>; las existentes (incluida Production, donde
/// <c>AccountingChartBackfillService.EnsureAsync</c> no corre) por el comando de despliegue
/// <c>backfill-opening-balance-posting-setup [apply]</c> (<see cref="MaintainOpeningBalancePostingSetupAsync"/>):
/// dry-run por defecto; con <c>apply</c> solo CREA lo que falta. Si 3.1.04 o 3.1.04.001 ya
/// existen con otro sentido (no son "Saldos de apertura" patrimonial acreedora) no se toca nada:
/// ni cuenta ni reglas — nunca se contabiliza la apertura contra una cuenta ajena. Una regla
/// existente (canónica o personalizada) nunca se modifica.
/// </summary>
public sealed partial class AccountingBootstrapStep
{
    internal const string OpeningBalanceGroupAccountCode = "3.1.04";
    internal const string OpeningBalanceAccountCode = "3.1.04.001";
    internal const string OpeningBalanceAccountName = "Saldos de apertura";
    private const string OpeningBalanceParentAccountCode = "3.1";

    internal const string OpeningBalanceCanonical = "Canonical";
    internal const string OpeningBalanceMissing = "Missing";

    private static bool IsOpeningBalanceAccountCode(string code) =>
        code is OpeningBalanceGroupAccountCode or OpeningBalanceAccountCode;

    private static bool IsOpeningBalanceRule(MinimalPostingRule rule) =>
        rule.SourceModule == OpeningBalancePostingFacts.SourceModule;

    private static IEnumerable<MinimalPostingRule> OpeningBalanceRules =>
        MinimalPostingRules.Where(IsOpeningBalanceRule);

    /// <summary>
    /// <c>null</c> si 3.1.04/3.1.04.001 no existen o ya son la cuenta puente (patrimonial,
    /// acreedora, "Saldos de apertura"); si no, la descripción del conflicto.
    /// </summary>
    internal static string? OpeningBalanceAccountConflict(
        IEnumerable<(string Code, string Name, AccountType Type, AccountNature Nature)> accounts
    )
    {
        var conflicts = accounts
            .Where(a => IsOpeningBalanceAccountCode(a.Code))
            .Where(a =>
                a.Type != AccountType.Equity
                || a.Nature != AccountNature.Credit
                || !string.Equals(
                    a.Name.Trim(),
                    OpeningBalanceAccountName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(a => a.Code, StringComparer.Ordinal)
            .Select(a => $"{a.Code} '{a.Name}' ({a.Type}/{a.Nature})")
            .ToList();
        return conflicts.Count == 0
            ? null
            : $"código ocupado por {string.Join(", ", conflicts)}";
    }

    // Bypass through the sanctioned PlatformQueryAccessor (no ambient tenant in a deployment
    // command); TenantId + CompanyId are re-applied explicitly in every query below.
    internal async Task<
        IReadOnlyList<(string Item, string Diagnostic)>
    > MaintainOpeningBalancePostingSetupAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        bool apply,
        CancellationToken cancellationToken
    )
    {
        var accounts = await _db
            .Accounts.AsPlatformQuery()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .ToListAsync(cancellationToken);
        var byCode = accounts.ToDictionary(a => a.Code.Value, StringComparer.Ordinal);
        var results = new List<(string, string)>();

        var conflict = OpeningBalanceAccountConflict(
            accounts.Select(a => (a.Code.Value, a.Name, a.AccountType, a.Nature))
        );
        if (conflict is not null)
        {
            LogOpeningBalanceAccountConflict(conflict, companyId);
            results.Add(($"Account {OpeningBalanceAccountCode}", $"Conflict: {conflict}; unchanged"));
            foreach (var rule in OpeningBalanceRules)
                results.Add(
                    (
                        $"{rule.SourceModule}/{rule.FactType}",
                        "Blocked: opening account conflict; unchanged"
                    )
                );
            return results;
        }

        var changed = false;
        var pendingAccountCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            var (code, parentCode, allowsPosting) in new[]
            {
                (OpeningBalanceGroupAccountCode, OpeningBalanceParentAccountCode, false),
                (OpeningBalanceAccountCode, OpeningBalanceGroupAccountCode, true),
            }
        )
        {
            var item = $"Account {code}";
            if (byCode.ContainsKey(code))
            {
                results.Add((item, OpeningBalanceCanonical));
                continue;
            }
            var parentPending = pendingAccountCodes.Contains(parentCode);
            if (!byCode.TryGetValue(parentCode, out var parent) && !parentPending)
            {
                results.Add((item, $"{OpeningBalanceMissing}; parent {parentCode} not found; unchanged"));
                continue;
            }
            if (!apply)
            {
                pendingAccountCodes.Add(code);
                results.Add((item, OpeningBalanceMissing));
                continue;
            }

            var account = Account.Create(
                tenantId,
                companyId,
                AccountCode.Create(code),
                OpeningBalanceAccountName,
                parent!.Id,
                AccountType.Equity,
                AccountNature.Credit,
                allowsPosting,
                actorId
            );
            _db.Accounts.Add(account);
            byCode[code] = account;
            changed = true;
            results.Add((item, $"{OpeningBalanceMissing} -> {OpeningBalanceCanonical}: created"));
        }

        var rules = await _db
            .PostingRules.AsPlatformQuery()
            .Include(r => r.Lines)
            .Where(r =>
                r.TenantId == tenantId
                && r.CompanyId == companyId
                && r.SourceModule == OpeningBalancePostingFacts.SourceModule
            )
            .ToListAsync(cancellationToken);
        var lookup = byCode.ToDictionary(
            kv => kv.Key,
            kv => new AccountSeedLookup(kv.Value.Id, kv.Value.IsActive, kv.Value.AllowsPosting),
            StringComparer.Ordinal
        );

        foreach (var canonical in OpeningBalanceRules)
        {
            var item = $"{canonical.SourceModule}/{canonical.FactType}";
            var matching = rules.Where(r => r.FactType == canonical.FactType).ToList();
            if (matching.Count > 0)
            {
                results.Add(
                    (
                        item,
                        matching.Count == 1 && IsCanonicalRule(matching[0], canonical, lookup)
                            ? OpeningBalanceCanonical
                            : "Custom: preserved; unchanged"
                    )
                );
                continue;
            }

            // Una cuenta puente que este mismo dry-run crearía no cuenta como inválida.
            var invalid = InvalidCanonicalAccounts(canonical, lookup)
                .Where(code => !pendingAccountCodes.Contains(code))
                .ToArray();
            if (invalid.Length > 0)
            {
                results.Add(
                    (item, $"{OpeningBalanceMissing}; InvalidAccounts: {string.Join(", ", invalid)}; unchanged")
                );
                continue;
            }
            if (!apply)
            {
                results.Add((item, OpeningBalanceMissing));
                continue;
            }

            var rule = PostingRule.Create(
                tenantId,
                companyId,
                canonical.SourceModule,
                canonical.FactType,
                null,
                null,
                null,
                actorId
            );
            foreach (var line in canonical.Lines)
                rule.AddLine(lookup[line.AccountCode].Id, line.Nature, line.AmountKind);
            _db.PostingRules.Add(rule);
            changed = true;
            results.Add((item, $"{OpeningBalanceMissing} -> {OpeningBalanceCanonical}: created"));
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);
        return results;
    }

    private static bool IsCanonicalRule(
        PostingRule rule,
        MinimalPostingRule canonical,
        Dictionary<string, AccountSeedLookup> accounts
    )
    {
        if (
            !rule.IsActive
            || rule.TaxCode is not null
            || rule.DebitAccountId is not null
            || rule.CreditAccountId is not null
            || rule.Lines.Count != canonical.Lines.Count
            || canonical.Lines.Any(l => !accounts.ContainsKey(l.AccountCode))
        )
            return false;
        var expected = canonical
            .Lines.Select(l => (accounts[l.AccountCode].Id, l.Nature, l.AmountKind))
            .ToHashSet();
        return expected.SetEquals(rule.Lines.Select(l => (l.AccountId, l.Nature, l.AmountKind)));
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Opening balance account not seeded for company {CompanyId}: {Conflict}. "
            + "InitialLoad opening posting rules are not seeded either (no bridge account)."
    )]
    private partial void LogOpeningBalanceAccountConflict(string conflict, Guid companyId);
}
