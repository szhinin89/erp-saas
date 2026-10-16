using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;

namespace ERP.Application.Modules.Expenses.UseCases.Documents;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — snapshot INFORMATIVO de la cuenta configurada en una subcategoría de
/// gasto. Nunca bloquea: una subcategoría sin cuenta, o con una cuenta inexistente en la empresa,
/// devuelve null. La validez contable (activa, postable, tipo gasto) se evalúa en cada intento de
/// contabilización, donde una carencia deja la obligación Failed sin afectar al gasto.
/// </summary>
internal static class ExpenseLineAccountSnapshot
{
    public static async Task<Account?> FindAsync(
        IAccountRepository accounts,
        Guid tenantId,
        Guid companyId,
        ExpenseCategoryNode subcategory,
        CancellationToken ct
    ) =>
        subcategory.AccountingAccountId is { } accountId
            ? await accounts.GetByIdAsync(tenantId, companyId, accountId, ct)
            : null;
}
