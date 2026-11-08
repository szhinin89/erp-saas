using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Services;

namespace ERP.Application.Modules.Accounting.UseCases.Accounts;

/// <summary>
/// ACCOUNTING-CHART-INTEGRITY-01 — invariantes del Plan de Cuentas que dependen de otras cuentas o
/// de configuraciones (accounting.md § Core y configuración contable). Se evalúan con el lock del
/// Plan de Cuentas ya adquirido. Las reglas de árbol viven en <see cref="AccountHierarchyRules"/>.
/// Rechazos de forma de la petición → 422; rechazos por el estado actual → 409 (mismo criterio
/// que el bloqueo de desactivación preexistente).
/// </summary>
internal static class AccountChartIntegrity
{
    private const int MaxListedReferences = 3;

    private const string InactiveAncestorReason = "una cuenta superior está inactiva. Actívela primero.";

    /// <summary>
    /// Crear una subcuenta (siempre nace activa): I4 sobre el padre y su cadena completa, y el padre
    /// no debe tener asientos publicados ni admitir movimiento.
    /// </summary>
    public static async Task<Result<AccountDto>?> ValidateNewChildAsync(
        IAccountUsageReader usage,
        Guid tenantId,
        Guid companyId,
        Account parent,
        IReadOnlyList<Account> accounts,
        CancellationToken ct
    )
    {
        if (AccountHierarchyRules.HasInactiveAncestor(parent.Id, accounts))
            return Result<AccountDto>.Conflict($"No se puede crear la cuenta: {InactiveAncestorReason}");
        if (await usage.HasPostedJournalLinesAsync(tenantId, companyId, parent.Id, ct))
            return Result<AccountDto>.Conflict(
                $"No se puede crear la subcuenta: la cuenta padre '{parent.Code.Value}' tiene asientos publicados."
            );
        if (parent.AllowsPosting)
            return Result<AccountDto>.ValidationFailure(
                $"La cuenta padre '{parent.Code.Value}' admite movimientos: desmarque primero 'Permite movimiento' para convertirla en agrupadora."
            );
        return null;
    }

    /// <summary>I1 (con subcuentas no admite movimiento) e I2 (quitar movimiento con referencias vigentes).</summary>
    public static async Task<Result<AccountDto>?> ValidateAllowsPostingAsync(
        IAccountUsageReader usage,
        Guid tenantId,
        Guid companyId,
        Account account,
        bool allowsPosting,
        IReadOnlyList<Account> accounts,
        CancellationToken ct
    )
    {
        if (allowsPosting && AccountHierarchyRules.HasChildren(account.Id, accounts))
            return Result<AccountDto>.ValidationFailure(
                "Una cuenta con subcuentas no puede admitir movimientos: desmarque 'Permite movimiento'."
            );
        if (!account.AllowsPosting || allowsPosting)
            return null;
        var conflict = await FindReferenceConflictAsync(
            usage,
            tenantId,
            companyId,
            account,
            "No se puede quitar 'Permite movimiento'",
            ct
        );
        return conflict is null ? null : Result<AccountDto>.Conflict(conflict);
    }

    /// <summary>I3 (descendientes activos de cualquier nivel) e I2 (referencias vigentes).</summary>
    public static async Task<Result<AccountDto>?> ValidateDisableAsync(
        IAccountUsageReader usage,
        Guid tenantId,
        Guid companyId,
        Account account,
        IReadOnlyList<Account> accounts,
        CancellationToken ct
    )
    {
        if (AccountHierarchyRules.HasActiveDescendant(account.Id, accounts))
            return Result<AccountDto>.Conflict(
                "No se puede desactivar: la cuenta tiene subcuentas activas. Desactívelas primero."
            );
        var conflict = await FindReferenceConflictAsync(
            usage,
            tenantId,
            companyId,
            account,
            "No se puede desactivar",
            ct
        );
        return conflict is null ? null : Result<AccountDto>.Conflict(conflict);
    }

    /// <summary>I4: ningún ancestro de la cadena completa puede estar inactivo.</summary>
    public static Result<AccountDto>? ValidateEnable(Account account, IReadOnlyList<Account> accounts) =>
        AccountHierarchyRules.HasInactiveAncestor(account, accounts)
            ? Result<AccountDto>.Conflict($"No se puede activar: {InactiveAncestorReason}")
            : null;

    private static async Task<string?> FindReferenceConflictAsync(
        IAccountUsageReader usage,
        Guid tenantId,
        Guid companyId,
        Account account,
        string blockedAction,
        CancellationToken ct
    )
    {
        var references = (
            await usage.GetConfigurationReferencesAsync(tenantId, companyId, account.Id, ct)
        ).ToList();
        // Regla existente del cierre anual: la cuenta de resultado debe estar activa y admitir
        // movimientos (FiscalYearClosingRules) — vigente siempre, sin fila de configuración.
        if (string.Equals(account.Code.Value, FiscalYearClosingRules.ResultAccountCode, StringComparison.Ordinal))
            references.Add(new(AccountReferenceFamily.FiscalYearResult, account.Code.Value));
        return references.Count == 0
            ? null
            : $"{blockedAction}: la cuenta está referenciada por {Describe(references)}.";
    }

    private static string Describe(List<AccountConfigurationReference> references)
    {
        var listed = string.Join(", ", references.Take(MaxListedReferences).Select(DescribeOne));
        var remaining = references.Count - MaxListedReferences;
        return remaining > 0 ? $"{listed} y {remaining} más" : listed;
    }

    private static string DescribeOne(AccountConfigurationReference reference) =>
        reference.Family switch
        {
            AccountReferenceFamily.PostingRule =>
                $"una Regla de Contabilización activa ({reference.Label})",
            AccountReferenceFamily.PaymentMethod => $"la forma de pago '{reference.Label}'",
            AccountReferenceFamily.CashRegister => $"la caja '{reference.Label}'",
            AccountReferenceFamily.CashMovementReason => $"el motivo de caja '{reference.Label}'",
            AccountReferenceFamily.ExpenseCategory => $"la categoría de gasto '{reference.Label}'",
            AccountReferenceFamily.CompanyBankAccount => $"la cuenta bancaria '{reference.Label}'",
            AccountReferenceFamily.InventoryAdjustmentReason =>
                $"el motivo de ajuste de inventario '{reference.Label}'",
            AccountReferenceFamily.FiscalYearResult =>
                "el cierre anual (cuenta de Resultado del ejercicio)",
            _ => $"una configuración contable ({reference.Label})",
        };
}
