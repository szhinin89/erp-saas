using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Services;

/// <summary>
/// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 1/8: invariantes del Plan de Cuentas canónico —
/// puro, sin dependencia de EF/DbContext, para poder testearse en Domain.Tests y reutilizarse
/// tanto en el backfill (Infrastructure) como en una auditoría antes/después (Fase 8). No muta
/// nada — solo reporta.
/// </summary>
public enum AccountHierarchyIssueType
{
    /// <summary>Invariante 1: código raíz (sin '.') con ParentAccountId asignado.</summary>
    OrphanRootWithParent,

    /// <summary>Invariantes 2/7: el código implica una cuenta agrupadora que no existe.</summary>
    MissingImmediateParent,

    /// <summary>Invariante 2: ParentAccountId no coincide con el padre canónico por código.</summary>
    ParentMismatch,

    /// <summary>Invariante 3: profundidad por ParentAccountId != profundidad por código.</summary>
    LevelMismatch,

    /// <summary>Invariante 4: cuenta con hijas tiene AllowsPosting=true.</summary>
    ParentAllowsPostingWithChildren,

    /// <summary>Invariante 6: ciclo en la cadena de ParentAccountId.</summary>
    CycleDetected,

    /// <summary>Invariante 5: cuenta referenciada por una PostingRule no es hoja activa y posteable.</summary>
    PostingRuleAccountInvalid,
}

public sealed record AccountHierarchyIssue(
    Guid AccountId,
    string Code,
    AccountHierarchyIssueType Type,
    string Detail
);

public sealed record AccountHierarchyReport(int TotalAccounts, IReadOnlyList<AccountHierarchyIssue> Issues)
{
    public int CountOf(AccountHierarchyIssueType type) => Issues.Count(i => i.Type == type);
}

public static class AccountHierarchyDiagnostics
{
    /// <summary>
    /// Analiza el Plan de Cuentas de una sola Company. <paramref name="postingRules"/> es
    /// opcional — sin ella, el invariante 5 (cuentas referenciadas por PostingRule) no se evalúa.
    /// </summary>
    public static AccountHierarchyReport Analyze(
        IReadOnlyList<Account> accounts,
        IReadOnlyList<PostingRule>? postingRules = null
    )
    {
        var issues = new List<AccountHierarchyIssue>();
        var byId = accounts.ToDictionary(a => a.Id);
        var byCode = accounts.ToDictionary(a => a.Code.Value, StringComparer.Ordinal);
        var childCountByParentId = accounts
            .Where(a => a.ParentAccountId is not null)
            .GroupBy(a => a.ParentAccountId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var account in accounts)
        {
            var code = account.Code.Value;
            var expectedParentCode = AccountHierarchyRules.GetExpectedParentCode(code);

            if (expectedParentCode is null)
            {
                if (account.ParentAccountId is not null)
                    issues.Add(
                        new(
                            account.Id,
                            code,
                            AccountHierarchyIssueType.OrphanRootWithParent,
                            "Código raíz (sin '.') con ParentAccountId asignado."
                        )
                    );
            }
            else if (!byCode.TryGetValue(expectedParentCode, out var expectedParent))
            {
                issues.Add(
                    new(
                        account.Id,
                        code,
                        AccountHierarchyIssueType.MissingImmediateParent,
                        $"Falta la cuenta agrupadora '{expectedParentCode}' implicada por el código."
                    )
                );
            }
            else if (account.ParentAccountId != expectedParent.Id)
            {
                issues.Add(
                    new(
                        account.Id,
                        code,
                        AccountHierarchyIssueType.ParentMismatch,
                        $"ParentAccountId no apunta a '{expectedParentCode}' (padre canónico por código)."
                    )
                );
            }

            var expectedDepth = AccountHierarchyRules.GetCodeDepth(code);
            var actualDepth = ComputeParentChainDepth(account, byId);
            if (actualDepth != expectedDepth)
                issues.Add(
                    new(
                        account.Id,
                        code,
                        AccountHierarchyIssueType.LevelMismatch,
                        $"Profundidad por ParentAccountId ({actualDepth}) != profundidad por código ({expectedDepth})."
                    )
                );

            var hasChildren =
                childCountByParentId.TryGetValue(account.Id, out var childCount) && childCount > 0;
            if (hasChildren && account.AllowsPosting)
                issues.Add(
                    new(
                        account.Id,
                        code,
                        AccountHierarchyIssueType.ParentAllowsPostingWithChildren,
                        "Cuenta con hijas tiene AllowsPosting=true (debe ser false)."
                    )
                );

            if (HasCycle(account, byId))
                issues.Add(
                    new(
                        account.Id,
                        code,
                        AccountHierarchyIssueType.CycleDetected,
                        "Ciclo detectado en la cadena de ParentAccountId."
                    )
                );
        }

        if (postingRules is not null)
        {
            var referencedAccountIds = new List<Guid>();
            foreach (var rule in postingRules)
            {
                if (rule.DebitAccountId is { } debitId)
                    referencedAccountIds.Add(debitId);
                if (rule.CreditAccountId is { } creditId)
                    referencedAccountIds.Add(creditId);
                referencedAccountIds.AddRange(rule.Lines.Select(l => l.AccountId));
            }

            foreach (var accountId in referencedAccountIds.Distinct())
            {
                if (!byId.TryGetValue(accountId, out var account))
                    continue;

                var isLeaf = !(
                    childCountByParentId.TryGetValue(accountId, out var cc) && cc > 0
                );
                if (!isLeaf || !account.IsActive || !account.AllowsPosting)
                    issues.Add(
                        new(
                            accountId,
                            account.Code.Value,
                            AccountHierarchyIssueType.PostingRuleAccountInvalid,
                            "Cuenta referenciada por una PostingRule no es hoja activa con AllowsPosting=true."
                        )
                    );
            }
        }

        return new AccountHierarchyReport(accounts.Count, issues);
    }

    private static int ComputeParentChainDepth(Account account, IReadOnlyDictionary<Guid, Account> byId)
    {
        var depth = 0;
        var current = account;
        var guard = byId.Count + 1;
        while (current.ParentAccountId is { } parentId && guard-- > 0)
        {
            if (!byId.TryGetValue(parentId, out var parent))
                break;
            depth++;
            current = parent;
        }
        return depth;
    }

    private static bool HasCycle(Account account, IReadOnlyDictionary<Guid, Account> byId)
    {
        var current = account.ParentAccountId;
        var guard = byId.Count + 1;
        while (current is { } id && guard-- > 0)
        {
            if (id == account.Id)
                return true;
            if (!byId.TryGetValue(id, out var parent))
                return false;
            current = parent.ParentAccountId;
        }
        return guard <= 0;
    }
}
