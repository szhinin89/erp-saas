namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo Facturación Electrónica (configuración SRI, certificados,
/// numeración y RIDE) — hub <c>/settings/electronic-invoicing</c>.
/// </summary>
public static class ElectronicInvoicingPermissions
{
    public const string View = "electronic-invoicing.view";
    public const string Configure = "electronic-invoicing.configure";
}
