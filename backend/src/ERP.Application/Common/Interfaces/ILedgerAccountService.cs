using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Resuelve cuentas del plan según la configuración por tenant.
/// Cuando no hay fila de <c>AccountingSetup</c>, los métodos de compra/venta devuelven éxito con valor null
/// (el llamador aplica la heurística por tipo de cuenta).
/// </summary>
public interface ILedgerAccountService
{
    /// <param name="inventorySubtotal">Base imponible de inventario (sin IVA).</param>
    /// <param name="iva">Monto de IVA de la compra.</param>
    Task<Result<JournalEntryAccounts?>> GetAccountsForPurchaseAsync(
        Guid subscriberId,
        decimal inventorySubtotal,
        decimal  vatTotal,
        CancellationToken ct);

    /// <param name="salesSubtotal">Base imponible de ventas (sin IVA).</param>
    Task<Result<JournalEntryAccounts?>> GetAccountsForSaleAsync(
        Guid subscriberId,
        decimal salesSubtotal,
        decimal  vatTotal,
        CancellationToken ct);

    /// <summary>Cuenta de gasto mapeada por categoría; null si no hay mapeo (usar heurística legacy).</summary>
    Task<Result<Guid?>> GetAccountForExpenseAsync(Guid subscriberId, string   category, CancellationToken ct);

    /// <summary>Caja o banco preferido para el crédito del asiento de gasto; null si no está configurado.</summary>
    Task<Result<Guid?>> GetCashAccountForExpenseAsync(Guid subscriberId, CancellationToken ct);
}
