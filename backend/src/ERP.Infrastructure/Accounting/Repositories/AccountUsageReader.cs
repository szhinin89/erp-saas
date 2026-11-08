using ERP.Application.Modules.Accounting.UseCases.Accounts;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

/// <summary>
/// ACCOUNTING-CHART-INTEGRITY-01 — usos de una cuenta que condicionan el Plan de Cuentas. Cada
/// consulta filtra explícitamente por tenant y empresa (además de los filtros globales del
/// contexto). Solo lectura; se invoca con el lock del Plan de Cuentas ya adquirido.
/// </summary>
public sealed class AccountUsageReader(ErpDbContext db) : IAccountUsageReader
{
    public async Task<IReadOnlyList<AccountConfigurationReference>> GetConfigurationReferencesAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        CancellationToken ct
    )
    {
        var references = new List<AccountConfigurationReference>();

        // Reglas de Contabilización: solo activas — EnablePostingRuleHandler revalida sus cuentas.
        references.AddRange(
            (
                await (
                    from line in db.PostingRuleLines
                    join rule in db.PostingRules on line.PostingRuleId equals rule.Id
                    where line.TenantId == tenantId && line.AccountId == accountId
                        && rule.TenantId == tenantId && rule.CompanyId == companyId
                        && rule.IsActive
                    select rule.SourceModule + "/" + rule.FactType
                ).Distinct().ToListAsync(ct)
            ).Select(label => new AccountConfigurationReference(AccountReferenceFamily.PostingRule, label))
        );

        // Familias 2–7: toda vinculación existente, de configuraciones activas o inactivas — su
        // reactivación no revalida la cuenta (no existe baja física ni desvinculación lógica).
        references.AddRange(
            (
                await (
                    from link in db.PaymentMethodAccounts
                    join method in db.PaymentMethods on link.PaymentMethodId equals method.Id
                    where link.TenantId == tenantId && link.CompanyId == companyId
                        && link.AccountingAccountId == accountId && method.TenantId == tenantId
                    select method.Name
                ).ToListAsync(ct)
            ).Select(label => new AccountConfigurationReference(AccountReferenceFamily.PaymentMethod, label))
        );

        references.AddRange(
            (
                await db.CashRegisters
                    .Where(x => x.TenantId == tenantId && x.CompanyId == companyId
                        && x.AccountingAccountId == accountId)
                    .Select(x => x.Name)
                    .ToListAsync(ct)
            ).Select(label => new AccountConfigurationReference(AccountReferenceFamily.CashRegister, label))
        );

        references.AddRange(
            (
                await db.CashMovementReasons
                    .Where(x => x.TenantId == tenantId && x.CompanyId == companyId
                        && x.AccountingAccountId == accountId)
                    .Select(x => x.Name)
                    .ToListAsync(ct)
            ).Select(label => new AccountConfigurationReference(AccountReferenceFamily.CashMovementReason, label))
        );

        references.AddRange(
            (
                await db.ExpenseCategoryNodes
                    .Where(x => x.TenantId == tenantId && x.CompanyId == companyId
                        && x.AccountingAccountId == accountId)
                    .Select(x => x.Name)
                    .ToListAsync(ct)
            ).Select(label => new AccountConfigurationReference(AccountReferenceFamily.ExpenseCategory, label))
        );

        references.AddRange(
            (
                await db.CompanyBankAccounts
                    .Where(x => x.TenantId == tenantId && x.CompanyId == companyId
                        && x.AccountingAccountId == accountId)
                    .Select(x => x.DisplayName)
                    .ToListAsync(ct)
            ).Select(label => new AccountConfigurationReference(AccountReferenceFamily.CompanyBankAccount, label))
        );

        references.AddRange(
            (
                await (
                    from link in db.InventoryAdjustmentReasonAccounts
                    join reason in db.InventoryAdjustmentReasons
                        on link.InventoryAdjustmentReasonId equals reason.Id
                    where link.TenantId == tenantId && link.CompanyId == companyId
                        && link.AccountingAccountId == accountId
                        && reason.TenantId == tenantId && reason.CompanyId == companyId
                    select reason.Name
                ).ToListAsync(ct)
            ).Select(label => new AccountConfigurationReference(AccountReferenceFamily.InventoryAdjustmentReason, label))
        );

        return references
            .OrderBy(r => r.Family)
            .ThenBy(r => r.Label, StringComparer.Ordinal)
            .ToList();
    }

    public Task<bool> HasPostedJournalLinesAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        CancellationToken ct
    ) =>
        (
            from line in db.JournalEntryLines
            join entry in db.JournalEntries on line.JournalEntryId equals entry.Id
            where line.TenantId == tenantId && line.AccountId == accountId
                && entry.TenantId == tenantId && entry.CompanyId == companyId
                && entry.Status == JournalEntryStatus.Posted
            select line.Id
        ).AnyAsync(ct);
}
