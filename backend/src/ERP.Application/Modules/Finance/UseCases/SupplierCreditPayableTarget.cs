using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;

namespace ERP.Application.Modules.Finance.UseCases;

/// <summary>
/// ZH-SUPPLIER-CREDIT-APPLY-PAYABLES-02D-C — resolución ÚNICA del destino de una aplicación (o de
/// su reversa) de <see cref="SupplierCredit"/>: el crédito aplica contra <see cref="AccountsPayable"/>
/// de cualquier origen soportado; el documento de origen solo decide cómo se serializa (Lock A) y de
/// dónde sale la moneda. Compartido por <see cref="ApplySupplierCreditHandler"/> y
/// <see cref="ReverseSupplierCreditApplicationHandler"/> — nunca una segunda implementación.
/// <list type="bullet">
/// <item><see cref="AccountsPayableOriginType.PurchaseInvoice"/> → Lock A oficial de Compras
/// (<c>"PurchaseInvoice.FinancialLock"</c>, el mismo de pago/devolución/retención/anulación); moneda
/// = <c>PurchaseInvoice.CurrencyCode</c>.</item>
/// <item><see cref="AccountsPayableOriginType.ExpenseDocument"/> → Gastos no tiene advisory lock
/// propio: su mecanismo oficial de concurrencia es el token <c>xmin</c> de
/// <see cref="AccountsPayable"/> (mismo que ya protege pagos a proveedor/anulación de gasto sobre
/// esa CxP; <c>Apply</c>/<c>Reverse</c> siempre actualizan la cabecera). Un choque concurrente se
/// rechaza en <c>SaveChanges</c> (SC-010), nunca se pierde. Moneda = <c>Company.CurrencyCode</c>
/// (<c>ExpenseDocument</c> no tiene moneda propia; misma SSOT que 02D-B).</item>
/// <item><see cref="AccountsPayableOriginType.InitialBalance"/> (IL-6B, saldo inicial de la Carga
/// Inicial de CxP) → no tiene documento de origen ERP que serializar ni moneda propia: mismo
/// tratamiento que Gastos — token <c>xmin</c> de la CxP y moneda = <c>Company.CurrencyCode</c>. Nunca
/// se inventa un documento de origen.</item>
/// <item>Cualquier otro origen (p. ej. <c>Manual</c>) → rechazo fail-closed.</item>
/// </list>
/// </summary>
internal static class SupplierCreditPayableTarget
{
    public const string NotFoundMessage = "La cuenta por pagar destino no existe.";

    /// <summary>
    /// Lock A del destino según su origen — SIEMPRE antes de Lock B del crédito (§15.4). Devuelve el
    /// mensaje de rechazo o <c>null</c> si el lock quedó adquirido (o el origen usa concurrencia
    /// optimista).
    /// </summary>
    public static async Task<string?> AcquireOriginLockAsync(
        IAccountsPayableRepository payables,
        IPurchaseReturnRepository purchaseReturns,
        Guid tenantId,
        Guid payableId,
        CancellationToken ct
    )
    {
        var origin = await payables.GetOriginAsync(tenantId, payableId, ct);
        if (origin is null)
            return NotFoundMessage;

        switch (origin.Value.OriginType)
        {
            case AccountsPayableOriginType.PurchaseInvoice:
                await purchaseReturns.AcquireFinancialLockAsync(
                    tenantId,
                    origin.Value.OriginId,
                    ct
                );
                return null;
            case AccountsPayableOriginType.ExpenseDocument:
            case AccountsPayableOriginType.InitialBalance:
                return null;
            default:
                return UnsupportedOriginMessage(origin.Value.OriginType);
        }
    }

    /// <summary>
    /// Revalidación bajo lock: misma empresa que el crédito (fail-closed, sin revelar la CxP ajena
    /// — <c>GetByIdAsync</c> de CxP solo filtra tenant) y origen soportado.
    /// </summary>
    public static string? ValidateOwnership(AccountsPayable payable, SupplierCredit credit)
    {
        if (payable.CompanyId != credit.CompanyId)
            return NotFoundMessage;
        return
            payable.OriginType
                is AccountsPayableOriginType.PurchaseInvoice
                    or AccountsPayableOriginType.ExpenseDocument
                    or AccountsPayableOriginType.InitialBalance
            ? null
            : UnsupportedOriginMessage(payable.OriginType);
    }

    /// <summary>Moneda del destino según su origen (<c>null</c> si no puede resolverse → rechazo).</summary>
    public static async Task<string?> ResolveCurrencyAsync(
        AccountsPayable payable,
        IPurchaseInvoiceRepository purchaseInvoices,
        ICompanyRepository companies,
        Guid tenantId,
        CancellationToken ct
    ) =>
        payable.OriginType switch
        {
            AccountsPayableOriginType.PurchaseInvoice => (
                await purchaseInvoices.GetByIdAsync(tenantId, payable.OriginId, ct)
            )?.CurrencyCode,
            AccountsPayableOriginType.ExpenseDocument or AccountsPayableOriginType.InitialBalance => (
                await companies.GetByIdAsync(payable.CompanyId, ct)
            )?.CurrencyCode,
            _ => null,
        };

    private static string UnsupportedOriginMessage(AccountsPayableOriginType originType) =>
        $"No se puede aplicar un saldo a favor de proveedor sobre una cuenta por pagar de origen {originType}.";
}
