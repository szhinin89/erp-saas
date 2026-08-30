using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Domain.Modules.Accounting.Services;

/// <summary>
/// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 7: nodo de árbol contable reutilizable para
/// reportes/diagramas jerárquicos futuros (Estado de Situación Financiera, Estado de Resultados,
/// Libro Mayor agrupado, futuros diagramas). No se expone por ningún endpoint todavía — es un
/// helper listo para que un caso de uso futuro no tenga que reimplementar el armado del árbol ni
/// la acumulación de saldos hacia las cuentas agrupadoras.
/// </summary>
public sealed record AccountTreeNode(
    Guid Id,
    string Code,
    string Name,
    AccountType AccountType,
    AccountNature Nature,
    bool AllowsPosting,
    bool IsActive,
    int Level,
    Guid? ParentAccountId,
    IReadOnlyList<AccountTreeNode> Children,
    decimal Balance
);

public static class AccountTreeBuilder
{
    /// <summary>
    /// Arma el árbol completo (todas las cuentas raíz con sus descendientes) en orden natural de
    /// código. <paramref name="balances"/> es opcional — sin ella, <see cref="AccountTreeNode.Balance"/>
    /// es 0 en cada nodo salvo la acumulación de hijas (útil solo si se pasan saldos). Cuando se
    /// pasa, el saldo de cada nodo es su propio saldo directo (si tiene) más la suma de los saldos
    /// acumulados de sus hijas — así una cuenta agrupadora sin movimiento propio igual refleja el
    /// total de lo que agrupa, para reportes/diagramas jerárquicos.
    /// </summary>
    public static IReadOnlyList<AccountTreeNode> Build(
        IReadOnlyList<Account> accounts,
        IReadOnlyDictionary<Guid, decimal>? balances = null
    )
    {
        var childrenByParentId = accounts
            .Where(a => a.ParentAccountId is not null)
            .GroupBy(a => a.ParentAccountId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Account>)g.ToList());

        var roots = accounts
            .Where(a => a.ParentAccountId is null)
            .OrderBy(a => a.Code.Value, AccountCodeComparer.Instance)
            .ToList();

        return roots.Select(a => BuildNode(a, 0, childrenByParentId, balances)).ToList();
    }

    private static AccountTreeNode BuildNode(
        Account account,
        int level,
        IReadOnlyDictionary<Guid, IReadOnlyList<Account>> childrenByParentId,
        IReadOnlyDictionary<Guid, decimal>? balances
    )
    {
        var childAccounts = childrenByParentId.TryGetValue(account.Id, out var kids)
            ? kids.OrderBy(a => a.Code.Value, AccountCodeComparer.Instance)
            : Enumerable.Empty<Account>();

        var children = childAccounts
            .Select(c => BuildNode(c, level + 1, childrenByParentId, balances))
            .ToList();

        var ownBalance = balances is not null && balances.TryGetValue(account.Id, out var b) ? b : 0m;
        var rolledUpBalance = ownBalance + children.Sum(c => c.Balance);

        return new AccountTreeNode(
            account.Id,
            account.Code.Value,
            account.Name,
            account.AccountType,
            account.Nature,
            account.AllowsPosting,
            account.IsActive,
            level,
            account.ParentAccountId,
            children,
            rolledUpBalance
        );
    }
}
