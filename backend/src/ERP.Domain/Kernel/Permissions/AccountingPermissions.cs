namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Estructura plana (Create/Update/Delete compartidos entre Account/AccountingPeriod/
/// PostingRule) — mismo patrón que <see cref="PricingPermissions"/>, sin granularidad por
/// sub-recurso. Delete se usa para baja lógica (Disable*), nunca para DELETE físico.
/// </summary>
public static class AccountingPermissions
{
    public const string View = "accounting.view";
    public const string Create = "accounting.create";
    public const string Update = "accounting.update";
    public const string Delete = "accounting.delete";

    /// <summary>
    /// DESTINOS-CONTABLES-COBROS-VENTAS-01 (permisos): "Cobros de ventas" (config. de
    /// PaymentMethodAccount) usaba <c>SalesPermissions.View/Update</c> — confundía el SSOT de
    /// navegación/permisos, ya que la pantalla vive bajo Contabilidad, no Ventas. Granularidad
    /// dedicada (en vez de reusar <see cref="View"/>/<see cref="Update"/> planos, que ya cubren
    /// Asientos/Plan de cuentas/Reglas contables/Reportes) porque este ticket lo pidió
    /// explícitamente para esta pantalla puntual — no es el criterio por defecto del módulo.
    /// </summary>
    public const string DestinationsSalesCollectionsView =
        "accounting.destinations.sales_collections.view";
    public const string DestinationsSalesCollectionsUpdate =
        "accounting.destinations.sales_collections.update";
}
