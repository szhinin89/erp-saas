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

    /// <summary>
    /// ACCOUNTING-CONTINUITY-01 — cambiar el modo operativo de Accounting de la empresa (versión
    /// nueva de la política con vigencia). Ver docs/architecture/accounting.md §3/§14.
    /// </summary>
    public const string SettingsUpdate = "accounting.settings.update";

    /// <summary>ACCOUNTING-CONTINUITY-01 — consultar obligaciones contables y su historial de intentos.</summary>
    public const string PostingsView = "accounting.postings.view";

    /// <summary>
    /// ACCOUNTING-CONTINUITY-01/02 — reproceso manual individual y en lote de obligaciones
    /// Pending/Failed. El permiso del documento origen no concede esta capacidad.
    /// </summary>
    public const string PostingsReprocess = "accounting.postings.reprocess";

    /// <summary>
    /// ACCOUNTING-07 — crear un período manual o generar los 12 períodos mensuales de un ejercicio.
    /// Dedicado: crear períodos sobre fechas pasadas reabre escrituras, no es una edición del plan
    /// de cuentas (<see cref="Create"/>).
    /// </summary>
    public const string PeriodsCreate = "accounting.periods.create";

    /// <summary>
    /// ACCOUNTING-07 — cerrar y bloquear períodos (irreversibles). Nombre aprobado en
    /// docs/architecture/accounting.md §21; reemplaza el uso transitorio de <see cref="Update"/>.
    /// </summary>
    public const string PeriodsClose = "accounting.periods.close";
}
