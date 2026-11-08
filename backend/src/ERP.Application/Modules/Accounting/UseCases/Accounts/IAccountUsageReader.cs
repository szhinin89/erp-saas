namespace ERP.Application.Modules.Accounting.UseCases.Accounts;

/// <summary>ACCOUNTING-CHART-INTEGRITY-01 — familias de configuración que vinculan una cuenta contable.</summary>
public enum AccountReferenceFamily
{
    PostingRule,
    PaymentMethod,
    CashRegister,
    CashMovementReason,
    ExpenseCategory,
    CompanyBankAccount,
    InventoryAdjustmentReason,
    FiscalYearResult,
}

/// <summary>Una configuración vigente que vincula la cuenta; <see cref="Label"/> identifica al propietario.</summary>
public sealed record AccountConfigurationReference(AccountReferenceFamily Family, string Label);

/// <summary>
/// ACCOUNTING-CHART-INTEGRITY-01 — lectura de los usos de una cuenta que condicionan el Plan de
/// Cuentas (accounting.md § Core y configuración contable). Toda consulta se acota a
/// <c>tenantId</c> + <c>companyId</c>.
/// </summary>
public interface IAccountUsageReader
{
    /// <summary>
    /// Configuraciones vigentes que vinculan la cuenta: líneas de Reglas de Contabilización
    /// <b>activas</b> (una inactiva se revalida al reactivarse) y toda vinculación existente de
    /// formas de pago, cajas, motivos de caja, categorías de gasto, cuentas bancarias y motivos de
    /// ajuste de inventario, activas o inactivas (su reactivación no revalida la cuenta). No incluye
    /// historia: asientos, copias de líneas de gasto ni transacciones registradas. La cuenta de
    /// resultado del ejercicio se resuelve por código en Application.
    /// </summary>
    Task<IReadOnlyList<AccountConfigurationReference>> GetConfigurationReferencesAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        CancellationToken ct
    );

    /// <summary>True si la cuenta tiene líneas en asientos publicados (incluidos los reversos, que
    /// también quedan publicados). Borradores y descartados no cuentan.</summary>
    Task<bool> HasPostedJournalLinesAsync(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        CancellationToken ct
    );
}
