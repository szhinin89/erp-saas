namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — permisos del módulo independiente de Pagos a Proveedores
/// (<c>SupplierPayment</c>). Deliberadamente distinto de <see cref="PayablesPermissions"/> (que solo
/// cubre lectura de <c>AccountsPayable</c>) y de <see cref="FinancePermissions"/> (Collections/CxC):
/// registrar/reversar un pago a proveedor es una acción propia, no un CRUD genérico de Finance.
/// <c>[NavItem]</c> vive en <see cref="ERP.Domain.Kernel.Modules.SuppliersModule"/> (grupo
/// <c>suppliers</c>, NAVIGATION-OPERATING-CYCLES-03), junto a Cuentas por Pagar — no bajo Gastos —
/// porque Pagos a Proveedores consume <c>AccountsPayable</c>, es cross-cutting igual que ella
/// (NAVIGATION-MENU-CLEANUP-PAYABLES-EXPENSES-01).
/// </summary>
public static class SupplierPaymentsPermissions
{
    public const string View = "supplier-payments.view";
    public const string Create = "supplier-payments.create";
    public const string Reverse = "supplier-payments.reverse";
}
