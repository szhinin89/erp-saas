namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — permisos del módulo independiente de Pagos a Proveedores
/// (<c>SupplierPayment</c>). Deliberadamente distinto de <see cref="PayablesPermissions"/> (que solo
/// cubre lectura de <c>AccountsPayable</c>) y de <see cref="FinancePermissions"/> (Collections/CxC):
/// registrar/reversar un pago a proveedor es una acción propia, no un CRUD genérico de Finance. Sin
/// <c>[NavItem]</c> todavía — esta fase es solo dominio/infraestructura, sin controller ni pantalla.
/// </summary>
public static class SupplierPaymentsPermissions
{
    public const string View = "supplier-payments.view";
    public const string Create = "supplier-payments.create";
    public const string Reverse = "supplier-payments.reverse";
}
