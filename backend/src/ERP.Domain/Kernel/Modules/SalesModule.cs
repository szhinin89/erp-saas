using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: reorganizado en Operación/Configuración/Reportes. Reporte de Ventas
// (antes ReportsModule) se movió aquí — mismos Ids/rutas/permisos.
// NAVIGATION-OPERATING-CYCLES-03: Cuentas por cobrar se movió a CustomersModule (ciclo cliente,
// no ciclo venta). Caja se fusionó aquí desde CajaModule.
//
// MAPA-MENU-ERP-SSOT-01: Caja se separa de vuelta a su propio módulo de nivel superior
// (TreasuryModule.cs, junto con Destinos financieros — el árbol objetivo agrupa todo lo
// financiero/de tesorería en un solo tile, no dentro de Ventas). El Monitor de Documentos
// Electrónicos se mueve a SriModule.cs (el árbol objetivo lo agrupa con Facturación electrónica
// bajo un tile "SRI/Documentos electrónicos" propio, ya que ambos son sobre el ciclo de vida del
// comprobante electrónico ante el SRI, no una operación de venta en sí). Ventas queda con:
// Facturas de venta/POS, Devoluciones, Preferencias de Ventas/POS, Reporte de Ventas — igual al
// árbol objetivo. Mismos Ids/rutas/permisos en los módulos nuevos, solo cambia a qué [Module]
// pertenecen.
[Module("sales", Icon = "💰", SortOrder = 70)]
public static class SalesModule
{
    // ── Ventas (MENU-FINAL-STRUCTURE-01: subgrupo renombrado de "Operación" al mismo
    // nombre del módulo) ─────────────────────────────────────────────
    [NavItem(
        "Ventas",
        LabelKey = "app.nav.item.sales.operation",
        SortOrder = 10,
        Id = "e4000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = SalesPermissions.View
    )]
    public const string OperationGroup = "/sales/operation-group";

    // ADMIN-PERMISSIONS-SSOT-KERNEL-02: Create/Update como acciones relacionadas — a diferencia de
    // CustomersModule.Receivables (mismo permiso base, pero solo lectura), esta pantalla sí crea/
    // edita ventas.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Ride.View/Regenerate (ver/regenerar el RIDE de la
    // factura — useRideActions.ts, consumido desde SalesPage.tsx) tampoco estaban en el catálogo
    // asignable; se agregan aquí (pantalla donde realmente se usan) en vez de crear un NavItem
    // propio para un permiso transversal de borde HTTP sin pantalla propia.
    [NavItem(
        "Facturas de venta / Punto de venta",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.invoices",
        SortOrder = 10,
        Id = "d1000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = SalesPermissions.Create + "," + SalesPermissions.Update
            + "," + RidePermissions.View + "," + RidePermissions.Regenerate
    )]
    public const string Invoices = "/sales";

    [NavItem(
        "Devoluciones de venta",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.returns",
        SortOrder = 20,
        Id = "d1000000-0000-4000-9000-000000000003",
        ParentId = "e4000000-0000-4000-9000-000000000010"
    )]
    public const string Returns = "/sales/returns";

    // MAPA-MENU-ERP-SSOT-01: "Electronic Documents Monitor" se movió a
    // SriModule.ElectronicDocumentsMonitor (mismo Id explícito nuevo, ver comentario allí — antes
    // derivaba su Id automáticamente de module.Code="sales", así que no había un Id previo que
    // preservar). Nueva ruta /sri/electronic-documents/monitor (antes
    // /electronic-documents/monitor, que queda como redirect en el frontend).

    // ── Configuración ────────────────────────────────────────────────
    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.sales.configuration",
        SortOrder = 20,
        Id = "e4000000-0000-4000-9000-000000000020",
        PermissionsAnyCsv = OperationalPreferencesPermissions.View
    )]
    public const string ConfigurationGroup = "/sales/configuration-group";

    // PAYMENT-METHOD-ACCOUNT-UI-NAV-01 / DESTINOS-CONTABLES-COBROS-VENTAS-01: "Métodos de Pago"
    // se retiró de aquí — quedaba como una segunda entrada al mismo destino que confundía más de
    // lo que ayudaba (ver decisión del usuario). Única entrada de menú ahora:
    // AccountingModule.SalesCollectionDestinations, bajo Contabilidad > Configuración >
    // Destinos contables > "Cobros de ventas" (mismo Id d1000000-0000-4000-9000-000000000002
    // reutilizado allí, sin generar un ui_nav_items huérfano).

    // Enlace contextual al tab "salesPos" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Ventas/POS",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.sales.posPreferences",
        SortOrder = 20,
        Id = "e4000000-0000-4000-9000-000000000021",
        ParentId = "e4000000-0000-4000-9000-000000000020"
    )]
    public const string PosPreferences = "/settings/operations?tab=salesPos";

    // MAPA-MENU-ERP-SSOT-01: "Cajas registradoras" y "Preferencias de Caja" se movieron a
    // TreasuryModule (Tesorería > Caja > Configuración) — mismos Ids, ver comentario allí.

    // ── Reportes ─────────────────────────────────────────────────────
    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.sales.reports",
        SortOrder = 30,
        Id = "e4000000-0000-4000-9000-000000000030",
        PermissionsAnyCsv = SalesPermissions.View
    )]
    public const string ReportsGroup = "/sales/reports-group";

    // Movido desde ReportsModule (antes /reportes/ventas en el grupo "reports" separado) —
    // mismo Id/ruta/permiso, ahora dentro de Ventas → Reportes.
    [NavItem(
        "Reporte de Ventas",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.reportes.ventas",
        SortOrder = 10,
        Id = "f7000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000030"
    )]
    public const string SalesReport = "/reportes/ventas";
}
